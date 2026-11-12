using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Guardou.Api.Models;
using Guardou.Application.Files;
using Guardou.Application.Users;
using Guardou.Domain.Files;
using Guardou.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Guardou.IntegrationTests;

public sealed class ItemsTests : SignedInTests
{
    [Fact]
    public async Task ItemsRequireASession()
    {
        HttpResponseMessage response = await Factory.CreateClient().GetAsync("/api/items/root", CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EachUserHasAnEmptyRootFolder()
    {
        ItemDetails root = await Client.GetItemAsync(ItemsApi.Root);
        StorageUsage? usage = await Client.GetFromJsonAsync<StorageUsage>("/api/items/usage", HttpClientExtensions.JsonOptions, CancellationToken);

        Assert.Equal(ItemKind.Folder, root.Entry.Kind);
        Assert.Null(root.Entry.ParentId);
        Assert.False(root.Entry.Permissions.CanModify);
        Assert.Empty(root.Items ?? []);
        Assert.Empty(root.Ancestors);
        Assert.Equal(0, usage?.UsedBytes);
        Assert.True(usage?.TotalBytes > 0);
    }

    [Fact]
    public async Task EntriesGiveTheCreator()
    {
        ItemEntry folder = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        ItemEntry file = await Client.UploadEntryAsync(folder.Id, "a.txt", "a");
        UserAccount admin = await Client.FindUserAsync(TestServerFactory.AdminUsername);
        ItemCreator expected = new(admin.Id, TestServerFactory.AdminDisplayName, HasPicture: false);

        ItemDetails details = await Client.GetItemAsync(folder.Id);

        Assert.Equal(expected, folder.Creator);
        Assert.Equal(expected, file.Creator);
        Assert.All(details.Items ?? [], entry => Assert.Equal(expected, entry.Creator));
    }

    [Fact]
    public async Task CreatesTheRootFolderWithTheUser()
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        ApplicationUser? bob = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByNameAsync(BobUsername);
        Item? root = await scope.ServiceProvider.GetRequiredService<IItemStore>().FindRootAsync(bob?.Id ?? string.Empty, CancellationToken);

        Assert.NotNull(root);
        Assert.Equal(ItemKind.Folder, root.Kind);
    }

    [Fact]
    public async Task ServesTheClientWithRevalidation()
    {
        HttpResponseMessage response = await Factory.CreateClient().GetAsync("/files", CancellationToken);

        Assert.True(response.Headers.CacheControl?.NoCache);
    }

    [Fact]
    public async Task ReadsTheRootFolderInParallel()
    {
        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(index =>
            Client.GetAsync(index % 2 == 0 ? "/api/items/root" : "/api/items/root/children?only=folders", CancellationToken)));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
    }

    [Fact]
    public async Task CreatesFolders()
    {
        ItemEntry docs = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        ItemEntry reports = await Client.CreateFolderEntryAsync(docs.Id, "reports");

        ItemDetails details = await Client.GetItemAsync(reports.Id);
        HttpResponseMessage duplicate = await Client.CreateFolderAsync(ItemsApi.Root, "docs");
        HttpResponseMessage invalid = await Client.CreateFolderAsync(ItemsApi.Root, "a/b");

        Assert.Equal(["", "docs"], details.Ancestors.Select(ancestor => ancestor.Name));
        Assert.Equal(docs.Id, reports.ParentId);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("application/problem+json", duplicate.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task StoresUploadsByDateWithGeneratedNames()
    {
        DateTimeOffset modified = new(2023, 7, 14, 12, 0, 0, TimeSpan.Zero);

        ItemEntry file = await ItemsApi.ReadEntryAsync(await Client.UploadAsync(ItemsApi.Root, "Report.TXT", "report", modified: modified));
        ItemDetails details = await Client.GetItemAsync(file.Id, "?content=true");

        Assert.Equal("Report.TXT", file.Name);
        Assert.Equal(modified, file.Modified);
        Assert.Equal("text/plain", file.MimeType);
        Assert.Equal("report", details.Content);
        string blob = Assert.Single(BlobFiles());
        Assert.Matches(StoragePattern("2023/07/14", "txt"), blob);
    }

    [Fact]
    public async Task UsesTheCurrentDateWithoutATimestamp()
    {
        await Client.UploadEntryAsync(ItemsApi.Root, "notes", "x");

        DateTimeOffset now = Factory.Time.GetUtcNow();
        Assert.Matches(StoragePattern(now.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture), extension: null), Assert.Single(BlobFiles()));
    }

    [Fact]
    public async Task CreatesTheFoldersOfARelativePath()
    {
        ItemEntry file = await ItemsApi.ReadEntryAsync(await Client.UploadAsync(ItemsApi.Root, "a.txt", "a", relativePath: "photos/2024/a.txt"));
        await Client.UploadEntryAsync(file.ParentId ?? ItemsApi.Root, "b.txt", "b");

        ItemDetails folder = await Client.GetItemAsync(file.ParentId ?? ItemsApi.Root);

        Assert.Equal("2024", folder.Entry.Name);
        Assert.Equal(["", "photos"], folder.Ancestors.Select(ancestor => ancestor.Name));
        Assert.Equal(["a.txt", "b.txt"], folder.Names());
    }

    [Fact]
    public async Task ResolvesUploadConflicts()
    {
        await Client.UploadEntryAsync(ItemsApi.Root, "a.txt", "first");

        HttpResponseMessage failed = await Client.UploadAsync(ItemsApi.Root, "a.txt", "second");
        HttpResponseMessage skipped = await Client.UploadAsync(ItemsApi.Root, "a.txt", "second", ConflictPolicy.Skip);
        ItemEntry renamed = await ItemsApi.ReadEntryAsync(await Client.UploadAsync(ItemsApi.Root, "a.txt", "second", ConflictPolicy.Rename));
        ItemEntry replaced = await ItemsApi.ReadEntryAsync(await Client.UploadAsync(ItemsApi.Root, "a.txt", "third", ConflictPolicy.Replace));
        IReadOnlyList<TrashEntry>? trash = await Client.GetFromJsonAsync<IReadOnlyList<TrashEntry>>("/api/trash", HttpClientExtensions.JsonOptions, CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, skipped.StatusCode);
        Assert.Equal("a (1).txt", renamed.Name);
        Assert.Equal("third", (await Client.GetItemAsync(replaced.Id, "?content=true")).Content);
        Assert.Equal(["a.txt"], trash?.Select(entry => entry.Entry.Name));
    }

    [Fact]
    public async Task RenamesItems()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "a.txt", "a");
        await Client.UploadEntryAsync(ItemsApi.Root, "b.txt", "b");

        HttpResponseMessage renamed = await Client.PatchAsJsonAsync($"/api/items/{file.Id}", new NameRequest("c.md"), CancellationToken);
        HttpResponseMessage conflict = await Client.PatchAsJsonAsync($"/api/items/{file.Id}", new NameRequest("b.txt"), CancellationToken);
        HttpResponseMessage root = await Client.PatchAsJsonAsync("/api/items/root", new NameRequest("x"), CancellationToken);

        ItemEntry entry = await ItemsApi.ReadEntryAsync(renamed);
        Assert.Equal("c.md", entry.Name);
        Assert.Equal("text/markdown", entry.MimeType);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, root.StatusCode);
    }

    [Fact]
    public async Task MovesItemsWithoutMovingBlobs()
    {
        ItemEntry docs = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "a.txt", "a");
        IReadOnlyList<string> blobs = BlobFiles();

        ItemEntry moved = await ItemsApi.ReadEntryAsync(await Client.TransferAsync("move", file.Id, docs.Id));
        HttpResponseMessage intoItself = await Client.TransferAsync("move", docs.Id, docs.Id);

        Assert.Equal(docs.Id, moved.ParentId);
        Assert.Equal(file.Id, moved.Id);
        Assert.Equal(["a.txt"], (await Client.GetItemAsync(docs.Id)).Names());
        Assert.Equal(blobs, BlobFiles());
        Assert.Equal(HttpStatusCode.BadRequest, intoItself.StatusCode);
    }

    [Fact]
    public async Task ConcurrentMovesDoNotMakeAParentCycle()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            ItemEntry first = await Client.CreateFolderEntryAsync(ItemsApi.Root, $"first {attempt}");
            ItemEntry second = await Client.CreateFolderEntryAsync(ItemsApi.Root, $"second {attempt}");

            HttpResponseMessage[] moves = await Task.WhenAll(
                Client.TransferAsync("move", first.Id, second.Id),
                Client.TransferAsync("move", second.Id, first.Id));

            Assert.Single(moves, response => response.IsSuccessStatusCode);
            IEnumerable<string> rootNames = (await Client.GetItemAsync(ItemsApi.Root)).Names();
            Assert.Single(rootNames, name => name == first.Name || name == second.Name);
        }
    }

    [Fact]
    public async Task ResolvesMoveConflicts()
    {
        ItemEntry docs = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        await Client.UploadEntryAsync(docs.Id, "a.txt", "old");
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "a.txt", "new");

        HttpResponseMessage failed = await Client.TransferAsync("move", file.Id, docs.Id);
        HttpResponseMessage skipped = await Client.TransferAsync("move", file.Id, docs.Id, ConflictPolicy.Skip);
        ItemEntry renamed = await ItemsApi.ReadEntryAsync(await Client.TransferAsync("move", file.Id, docs.Id, ConflictPolicy.Rename));

        Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, skipped.StatusCode);
        Assert.Equal("a (1).txt", renamed.Name);
    }

    [Fact]
    public async Task CannotReplaceAnAncestorOfTheSource()
    {
        ItemEntry docs = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        ItemEntry inner = await Client.CreateFolderEntryAsync(docs.Id, "docs");

        HttpResponseMessage response = await Client.TransferAsync("move", inner.Id, ItemsApi.Root, ConflictPolicy.Replace);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CopiesFoldersWithNewBlobs()
    {
        ItemEntry docs = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        ItemEntry reports = await Client.CreateFolderEntryAsync(docs.Id, "reports");
        await Client.UploadEntryAsync(reports.Id, "a.txt", "a");

        ItemEntry copy = await ItemsApi.ReadEntryAsync(await Client.TransferAsync("copy", docs.Id, ItemsApi.Root, ConflictPolicy.Rename));
        ItemDetails copiedDocs = await Client.GetItemAsync(copy.Id);
        ItemDetails copiedReports = await Client.GetItemAsync(Assert.Single(copiedDocs.Items ?? []).Id);
        ItemEntry copiedFile = Assert.Single(copiedReports.Items ?? []);

        Assert.Equal("docs (1)", copy.Name);
        Assert.NotEqual(reports.Id, copiedReports.Entry.Id);
        Assert.Equal("a", (await Client.GetItemAsync(copiedFile.Id, "?content=true")).Content);
        Assert.Equal(2, BlobFiles().Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.TransferAsync("copy", docs.Id, reports.Id)).StatusCode);
    }

    [Fact]
    public async Task FiltersChildren()
    {
        await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        await Client.UploadEntryAsync(ItemsApi.Root, "a.txt", "a");

        IReadOnlyList<ItemEntry>? folders = await Client.GetFromJsonAsync<IReadOnlyList<ItemEntry>>(
            "/api/items/root/children?only=folders", HttpClientExtensions.JsonOptions, CancellationToken);

        Assert.Equal(["docs"], folders?.Select(item => item.Name));
    }

    [Fact]
    public async Task UsersCannotSeeTheItemsOfOtherUsers()
    {
        ItemEntry secret = await Client.UploadEntryAsync(ItemsApi.Root, "secret.txt", "secret");
        HttpClient bob = await SignInAsBobAsync();

        ItemDetails root = await bob.GetItemAsync(ItemsApi.Root);
        HttpResponseMessage direct = await bob.GetAsync($"/api/items/{secret.Id}", CancellationToken);
        HttpResponseMessage download = await bob.GetAsync($"/api/items/{secret.Id}/download", CancellationToken);
        HttpResponseMessage invalid = await bob.GetAsync("/api/items/not-an-id", CancellationToken);

        Assert.Empty(root.Items ?? []);
        Assert.Equal(HttpStatusCode.NotFound, direct.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    private static Regex StoragePattern(string day, string? extension)
    {
        string suffix = extension is null ? string.Empty : $"\\.{extension}";
        return new Regex($"^[0-9a-f-]{{36}}/{day}/[0-9a-f]{{32}}{suffix}$");
    }
}
