using System.Net;
using Guardou.Application.Files;
using Guardou.Domain.Files;

namespace Guardou.IntegrationTests;

public sealed class PreviewsTests : SignedInTests
{
    [Fact]
    public async Task CreatesWebpPreviewsAfterAnUpload()
    {
        ItemEntry image = await Client.UploadImageAsync("wide.png");

        HttpResponseMessage thumbnail = await Client.WaitForPreviewAsync(image.Id);
        HttpResponseMessage large = await Client.WaitForPreviewAsync(image.Id, "large");

        Assert.True(image.HasPreview);
        Assert.Equal(HttpStatusCode.OK, thumbnail.StatusCode);
        Assert.Equal("image/webp", thumbnail.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, large.StatusCode);
        Assert.True(thumbnail.Headers.CacheControl?.Private);
        Assert.Equal(TimeSpan.FromDays(365), thumbnail.Headers.CacheControl?.MaxAge);
        Assert.Contains("immutable", thumbnail.Headers.CacheControl?.Extensions.Select(extension => extension.Name) ?? []);
    }

    [Fact]
    public async Task NamesThePreviewFilesWithTheItemId()
    {
        ItemEntry image = await Client.UploadImageAsync("wide.png");
        await Client.WaitForIdleQueuesAsync();

        string itemId = ItemId.TryParse(image.Id, out Guid id) ? id.ToString("N") : string.Empty;
        Assert.Equal(
            [$"{itemId}.large.webp", $"{itemId}.thumbnail.webp"],
            ThumbFiles().Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task KeepsTheExistingPreviewsWhenAllPreviewsAreRequested()
    {
        ItemEntry image = await Client.UploadImageAsync("wide.png");
        await Client.WaitForIdleQueuesAsync();
        DateTime written = File.GetLastWriteTimeUtc(ThumbFiles()[0]);

        await Client.PostAsync("/api/tasks/queues/preview/all", null, CancellationToken);
        await Client.WaitForIdleQueuesAsync();

        Assert.Equal(written, File.GetLastWriteTimeUtc(ThumbFiles()[0]));
    }

    [Fact]
    public async Task RendersThePreviewAgainWhenTheCacheIsDeleted()
    {
        ItemEntry image = await Client.UploadImageAsync("wide.png");
        await Client.WaitForPreviewAsync(image.Id);
        await Client.WaitForIdleQueuesAsync();
        Directory.Delete(Factory.ThumbsDirectory, recursive: true);

        HttpResponseMessage pending = await Client.GetPreviewAsync(image.Id);
        HttpResponseMessage rendered = await Client.WaitForPreviewAsync(image.Id);

        Assert.Equal(HttpStatusCode.Accepted, pending.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rendered.StatusCode);
    }

    [Fact]
    public async Task HasNoPreviewForOtherFiles()
    {
        ItemEntry text = await Client.UploadEntryAsync(ItemsApi.Root, "notes.txt", "text");
        ItemEntry folder = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");

        Assert.False(text.HasPreview);
        Assert.False(folder.HasPreview);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetPreviewAsync(text.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetPreviewAsync(folder.Id)).StatusCode);
    }

    [Fact]
    public async Task HidesThePreviewsOfOtherUsers()
    {
        ItemEntry image = await Client.UploadImageAsync("wide.png");
        await Client.WaitForPreviewAsync(image.Id);
        using HttpClient bob = await SignInAsBobAsync();

        HttpResponseMessage response = await bob.GetPreviewAsync(image.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReturnsNotFoundWhenThePreviewFails()
    {
        ItemEntry broken = await ItemsApi.ReadEntryAsync(await Client.UploadAsync(ItemsApi.Root, "broken.png", "not an image"));

        HttpResponseMessage response = await Client.WaitForPreviewAsync(broken.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreatesPreviewsForCopies()
    {
        ItemEntry image = await Client.UploadImageAsync("wide.png");
        ItemEntry folder = await Client.CreateFolderEntryAsync(ItemsApi.Root, "copies");

        ItemEntry copy = await ItemsApi.ReadEntryAsync(await Client.TransferAsync("copy", image.Id, folder.Id));

        Assert.Equal(HttpStatusCode.OK, (await Client.WaitForPreviewAsync(copy.Id)).StatusCode);
    }

    [Fact]
    public async Task DeletesPreviewsWithTheFile()
    {
        ItemEntry image = await Client.UploadImageAsync("wide.png");
        await Client.WaitForPreviewAsync(image.Id);
        await Client.WaitForPreviewAsync(image.Id, "large");
        int cached = ThumbFiles().Count;

        await Client.PostSelectionAsync("/api/items/trash", image.Id);
        await Client.PostSelectionAsync("/api/trash/delete", image.Id);

        Assert.Equal(2, cached);
        Assert.Empty(ThumbFiles());
    }

    private List<string> ThumbFiles()
    {
        return Directory.Exists(Factory.ThumbsDirectory)
            ? Directory.EnumerateFiles(Factory.ThumbsDirectory, "*", SearchOption.AllDirectories).ToList()
            : [];
    }
}
