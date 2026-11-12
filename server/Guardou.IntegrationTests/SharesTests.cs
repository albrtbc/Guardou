using System.Net;
using System.Net.Http.Json;
using Guardou.Application.Files;
using Guardou.Application.Shares;
using Guardou.Application.Users;
using Guardou.Domain.Settings;

namespace Guardou.IntegrationTests;

public sealed class SharesTests : SignedInTests
{
    private const string LinkPrefix = "http://localhost/s/";

    [Fact]
    public async Task SharesAFileWithOneLink()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "report.txt", "0123456789");

        ShareLink first = await ShareAsync(file.Id);
        ShareLink second = await ShareAsync(file.Id);

        Assert.StartsWith(LinkPrefix, first.Url, StringComparison.Ordinal);
        Assert.Equal(first, second);
        Assert.Equal(first, await GetShareAsync(file.Id));
    }

    [Fact]
    public async Task UsesTheExternalUrlForTheLink()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "report.txt", "a");
        await Client.PutAsJsonAsync("/api/settings/server", new ServerSettings("https://files.example.com"), CancellationToken);

        ShareLink link = await ShareAsync(file.Id);

        Assert.StartsWith("https://files.example.com/s/", link.Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReturnsNotFoundForAnItemThatIsNotShared()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "report.txt", "a");

        HttpResponseMessage response = await Client.GetAsync($"/api/items/{file.Id}/share", CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RefusesToShareTheRootFolder()
    {
        HttpResponseMessage response = await Client.PutAsync($"/api/items/{ItemsApi.Root}/share", null, CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GivesAnonymousReadAccessToAFolderTree()
    {
        ItemEntry docs = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        ItemEntry inner = await Client.CreateFolderEntryAsync(docs.Id, "inner");
        ItemEntry file = await Client.UploadEntryAsync(inner.Id, "a.txt", "a");
        string token = TokenOf(await ShareAsync(docs.Id));
        using HttpClient anonymous = Factory.CreateClient();

        ItemDetails root = await GetSharedDetailsAsync(anonymous, token, docs.Id);
        ItemDetails folder = await GetSharedDetailsAsync(anonymous, token, inner.Id);
        HttpResponseMessage download = await anonymous.GetAsync($"/api/shares/{token}/items/{file.Id}/download", CancellationToken);

        Assert.Null(root.Entry.ParentId);
        Assert.Empty(root.Ancestors);
        Assert.Equal(["inner"], root.Names());
        Assert.Equal([docs.Id], folder.Ancestors.Select(crumb => crumb.Id));
        Assert.Equal(docs.Id, folder.Entry.ParentId);
        Assert.All(folder.Items ?? [], entry => Assert.False(entry.Permissions.CanModify));
        Assert.Equal("a", await download.Content.ReadAsStringAsync(CancellationToken));
    }

    [Fact]
    public async Task DownloadsASharedFolderAsAZip()
    {
        ItemEntry docs = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        await Client.UploadEntryAsync(docs.Id, "a.txt", "a");
        string token = TokenOf(await ShareAsync(docs.Id));

        HttpResponseMessage response = await Factory.CreateClient().GetAsync($"/api/shares/{token}/items/{docs.Id}/download", CancellationToken);

        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("docs.zip", response.Content.Headers.ContentDisposition?.FileNameStar);
    }

    [Fact]
    public async Task RefusesItemsOutsideTheSharedFolder()
    {
        ItemEntry docs = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        ItemEntry secret = await Client.UploadEntryAsync(ItemsApi.Root, "secret.txt", "b");
        ItemDetails home = await Client.GetItemAsync(ItemsApi.Root);
        string token = TokenOf(await ShareAsync(docs.Id));
        using HttpClient anonymous = Factory.CreateClient();

        HttpResponseMessage sibling = await anonymous.GetAsync($"/api/shares/{token}/items/{secret.Id}", CancellationToken);
        HttpResponseMessage parent = await anonymous.GetAsync($"/api/shares/{token}/items/{home.Entry.Id}", CancellationToken);
        HttpResponseMessage alias = await anonymous.GetAsync($"/api/shares/{token}/items/{ItemsApi.Root}", CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, sibling.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, parent.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, alias.StatusCode);
    }

    [Fact]
    public async Task HidesATrashedSubfolderOfASharedFolder()
    {
        ItemEntry docs = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        ItemEntry inner = await Client.CreateFolderEntryAsync(docs.Id, "inner");
        ItemEntry file = await Client.UploadEntryAsync(inner.Id, "a.txt", "a");
        string token = TokenOf(await ShareAsync(docs.Id));

        await Client.PostSelectionAsync("/api/items/trash", inner.Id);
        using HttpClient anonymous = Factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/shares/{token}/items/{file.Id}/download", CancellationToken)).StatusCode);
        Assert.Empty((await GetSharedDetailsAsync(anonymous, token, docs.Id)).Items ?? []);
    }

    [Fact]
    public async Task DoesNotShareTheFileOfAnotherUser()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "report.txt", "a");
        using HttpClient bob = await SignInAsBobAsync();

        HttpResponseMessage response = await bob.PutAsync($"/api/items/{file.Id}/share", null, CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GivesAnonymousReadAccess()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "report.txt", "0123456789");
        string token = TokenOf(await ShareAsync(file.Id));
        using HttpClient anonymous = Factory.CreateClient();

        ItemEntry? shared = await anonymous.GetFromJsonAsync<ItemEntry>($"/api/shares/{token}", HttpClientExtensions.JsonOptions, CancellationToken);
        HttpResponseMessage download = await anonymous.GetAsync($"/api/shares/{token}/items/{file.Id}/download", CancellationToken);
        HttpResponseMessage view = await anonymous.GetAsync($"/api/shares/{token}/items/{file.Id}/view", CancellationToken);

        Assert.Equal(file.Id, shared?.Id);
        Assert.Null(shared?.ParentId);
        Assert.Equal(new ItemPermissions(CanModify: false, CanAddChildren: false), shared?.Permissions);
        Assert.Equal("0123456789", await download.Content.ReadAsStringAsync(CancellationToken));
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("inline", view.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("sandbox", Assert.Single(view.Headers.GetValues("Content-Security-Policy")));
    }

    [Fact]
    public async Task RefusesAnotherItemThroughTheToken()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "report.txt", "a");
        ItemEntry other = await Client.UploadEntryAsync(ItemsApi.Root, "secret.txt", "b");
        string token = TokenOf(await ShareAsync(file.Id));
        using HttpClient anonymous = Factory.CreateClient();

        HttpResponseMessage response = await anonymous.GetAsync($"/api/shares/{token}/items/{other.Id}/download", CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA")]
    public async Task ReturnsNotFoundForAnUnknownToken(string token)
    {
        using HttpClient anonymous = Factory.CreateClient();

        HttpResponseMessage response = await anonymous.GetAsync($"/api/shares/{token}", CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task StopsSharingAndGivesANewTokenNextTime()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "report.txt", "a");
        string token = TokenOf(await ShareAsync(file.Id));

        HttpResponseMessage stop = await Client.DeleteAsync($"/api/items/{file.Id}/share", CancellationToken);
        HttpResponseMessage old = await Factory.CreateClient().GetAsync($"/api/shares/{token}", CancellationToken);
        string renewed = TokenOf(await ShareAsync(file.Id));

        Assert.Equal(HttpStatusCode.NoContent, stop.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, old.StatusCode);
        Assert.NotEqual(token, renewed);
    }

    [Fact]
    public async Task DeletesTheSharesOfFilesInATrashedFolder()
    {
        ItemEntry docs = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        ItemEntry file = await Client.UploadEntryAsync(docs.Id, "report.txt", "a");
        string token = TokenOf(await ShareAsync(file.Id));

        await Client.PostSelectionAsync("/api/items/trash", docs.Id);
        await Client.PostSelectionAsync("/api/trash/restore", docs.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await Factory.CreateClient().GetAsync($"/api/shares/{token}", CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/items/{file.Id}/share", CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task DeletesTheShareOfAReplacedFile()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "report.txt", "a");
        string token = TokenOf(await ShareAsync(file.Id));

        await Client.UploadAsync(ItemsApi.Root, "report.txt", "b", ConflictPolicy.Replace);

        Assert.Equal(HttpStatusCode.NotFound, (await Factory.CreateClient().GetAsync($"/api/shares/{token}", CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task DeletesTheShareWithTheFile()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "report.txt", "a");
        await ShareAsync(file.Id);
        await Client.PostSelectionAsync("/api/items/trash", file.Id);

        HttpResponseMessage response = await Client.PostSelectionAsync("/api/trash/delete", file.Id);

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ShowsTheCreatorOfSharedItems()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "report.txt", "a");
        UserAccount admin = await Client.FindUserAsync(TestServerFactory.AdminUsername);
        string token = TokenOf(await ShareAsync(file.Id));
        using HttpClient anonymous = Factory.CreateClient();
        HttpResponseMessage missing = await anonymous.GetAsync($"/api/shares/{token}/items/{file.Id}/creator/picture", CancellationToken);

        await Client.ReplacePictureAsync(admin.Id, PreviewsApi.Png(64, 64));
        ItemEntry? shared = await anonymous.GetFromJsonAsync<ItemEntry>($"/api/shares/{token}", HttpClientExtensions.JsonOptions, CancellationToken);
        HttpResponseMessage picture = await anonymous.GetAsync($"/api/shares/{token}/items/{file.Id}/creator/picture", CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(new ItemCreator(Id: null, TestServerFactory.AdminDisplayName, HasPicture: true), shared?.Creator);
        Assert.Equal("image/webp", picture.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task HidesTheCreatorIdFromAnonymousVisitors()
    {
        ItemEntry docs = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        await Client.UploadEntryAsync(docs.Id, "a.txt", "a");
        string token = TokenOf(await ShareAsync(docs.Id));
        using HttpClient anonymous = Factory.CreateClient();

        ItemDetails folder = await GetSharedDetailsAsync(anonymous, token, docs.Id);
        string json = await anonymous.GetStringAsync($"/api/shares/{token}/items/{docs.Id}", CancellationToken);

        Assert.Null(folder.Entry.Creator.Id);
        Assert.NotEmpty(folder.Items ?? []);
        Assert.All(folder.Items ?? [], entry => Assert.Null(entry.Creator.Id));
        Assert.Equal(TestServerFactory.AdminDisplayName, folder.Entry.Creator.DisplayName);
        Assert.DoesNotContain((await Client.FindUserAsync(TestServerFactory.AdminUsername)).Id, json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DoesNotCacheSharedFilesAndFolderArchives()
    {
        ItemEntry docs = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        ItemEntry file = await Client.UploadEntryAsync(docs.Id, "a.txt", "a");
        string token = TokenOf(await ShareAsync(docs.Id));
        using HttpClient anonymous = Factory.CreateClient();

        HttpResponseMessage view = await anonymous.GetAsync($"/api/shares/{token}/items/{file.Id}/view", CancellationToken);
        HttpResponseMessage download = await anonymous.GetAsync($"/api/shares/{token}/items/{file.Id}/download", CancellationToken);
        HttpResponseMessage archive = await anonymous.GetAsync($"/api/shares/{token}/items/{docs.Id}/download", CancellationToken);

        AssertNoStore(view);
        AssertNoStore(download);
        AssertNoStore(archive);
        Assert.Equal("application/zip", archive.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CachesSharedPreviewsForFiveMinutes()
    {
        ItemEntry image = await Client.UploadImageAsync("wide.png");
        await Client.WaitForPreviewAsync(image.Id);
        string token = TokenOf(await ShareAsync(image.Id));
        using HttpClient anonymous = Factory.CreateClient();

        HttpResponseMessage preview = await anonymous.GetAsync($"/api/shares/{token}/items/{image.Id}/preview?size=thumbnail", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.True(preview.Headers.CacheControl?.Private);
        Assert.Equal(TimeSpan.FromMinutes(5), preview.Headers.CacheControl?.MaxAge);
        Assert.DoesNotContain("immutable", preview.Headers.CacheControl?.Extensions.Select(extension => extension.Name) ?? []);
    }

    [Fact]
    public async Task RefusesItemsOutsideTheShareOnViewPreviewAndArchive()
    {
        ItemEntry docs = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        ItemEntry secretFolder = await Client.CreateFolderEntryAsync(ItemsApi.Root, "secret");
        ItemEntry secretImage = await Client.UploadImageAsync("secret.png");
        await Client.WaitForPreviewAsync(secretImage.Id);
        string token = TokenOf(await ShareAsync(docs.Id));
        using HttpClient anonymous = Factory.CreateClient();

        HttpResponseMessage view = await anonymous.GetAsync($"/api/shares/{token}/items/{secretImage.Id}/view", CancellationToken);
        HttpResponseMessage preview = await anonymous.GetAsync($"/api/shares/{token}/items/{secretImage.Id}/preview?size=thumbnail", CancellationToken);
        HttpResponseMessage archive = await anonymous.GetAsync($"/api/shares/{token}/items/{secretFolder.Id}/download", CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, view.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, preview.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, archive.StatusCode);
    }

    [Fact]
    public async Task RefusesTheCreatorPictureOfAnItemOutsideTheShare()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "report.txt", "a");
        ItemEntry other = await Client.UploadEntryAsync(ItemsApi.Root, "other.txt", "b");
        UserAccount admin = await Client.FindUserAsync(TestServerFactory.AdminUsername);
        await Client.ReplacePictureAsync(admin.Id, PreviewsApi.Png(64, 64));
        string token = TokenOf(await ShareAsync(file.Id));

        HttpResponseMessage response = await Factory.CreateClient().GetAsync($"/api/shares/{token}/items/{other.Id}/creator/picture", CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ExpiresTheLinkAtTheExpiryDate()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "report.txt", "a");
        string token = TokenOf(await ShareAsync(file.Id));
        DateTimeOffset now = Factory.Time.GetUtcNow();
        DateTimeOffset expires = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, TimeSpan.Zero).AddDays(1);
        using HttpClient anonymous = Factory.CreateClient();
        using HttpClient bob = await SignInAsBobAsync();

        ShareLink changed = await ReadLinkAsync(await ChangeExpiryAsync(file.Id, expires));
        HttpResponseMessage before = await anonymous.GetAsync($"/api/shares/{token}", CancellationToken);
        Factory.Time.Advance(TimeSpan.FromDays(2));
        HttpResponseMessage details = await anonymous.GetAsync($"/api/shares/{token}", CancellationToken);
        HttpResponseMessage download = await anonymous.GetAsync($"/api/shares/{token}/items/{file.Id}/download", CancellationToken);
        await bob.RefreshAntiforgeryTokenAsync();
        HttpResponseMessage join = await bob.PostAsync($"/api/shares/{token}/join", null, CancellationToken);

        Assert.Equal(expires, changed.Expires);
        Assert.Equal(expires, (await GetShareAsync(file.Id)).Expires);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, details.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, join.StatusCode);
    }

    [Fact]
    public async Task RemovesTheExpiryDate()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "report.txt", "a");
        string token = TokenOf(await ShareAsync(file.Id));
        await ChangeExpiryAsync(file.Id, Factory.Time.GetUtcNow().AddDays(1));

        ShareLink changed = await ReadLinkAsync(await ChangeExpiryAsync(file.Id, null));
        Factory.Time.Advance(TimeSpan.FromDays(2));
        using HttpClient anonymous = Factory.CreateClient();
        HttpResponseMessage details = await anonymous.GetAsync($"/api/shares/{token}", CancellationToken);

        Assert.Null(changed.Expires);
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
    }

    [Fact]
    public async Task RefusesAnExpiryDateInThePastOrWithoutALink()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "report.txt", "a");
        DateTimeOffset tomorrow = Factory.Time.GetUtcNow().AddDays(1);

        HttpResponseMessage notShared = await ChangeExpiryAsync(file.Id, tomorrow);
        await ShareAsync(file.Id);
        HttpResponseMessage past = await ChangeExpiryAsync(file.Id, Factory.Time.GetUtcNow().AddMinutes(-1));

        Assert.Equal(HttpStatusCode.NotFound, notShared.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
    }

    private Task<HttpResponseMessage> ChangeExpiryAsync(string id, DateTimeOffset? expires)
    {
        return Client.PutAsJsonAsync($"/api/items/{id}/share/expiry", new ShareExpiry(expires), HttpClientExtensions.JsonOptions, CancellationToken);
    }

    private static void AssertNoStore(HttpResponseMessage response)
    {
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    private static string TokenOf(ShareLink link)
    {
        return link.Url[(link.Url.LastIndexOf('/') + 1)..];
    }

    private async Task<ShareLink> ShareAsync(string id)
    {
        HttpResponseMessage response = await Client.PutAsync($"/api/items/{id}/share", null, CancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadLinkAsync(response);
    }

    private async Task<ShareLink> GetShareAsync(string id)
    {
        HttpResponseMessage response = await Client.GetAsync($"/api/items/{id}/share", CancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadLinkAsync(response);
    }

    private static async Task<ItemDetails> GetSharedDetailsAsync(HttpClient client, string token, string id)
    {
        return await client.GetFromJsonAsync<ItemDetails>($"/api/shares/{token}/items/{id}", HttpClientExtensions.JsonOptions, CancellationToken)
            ?? throw new InvalidOperationException("The shared item is missing.");
    }

    private static async Task<ShareLink> ReadLinkAsync(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<ShareLink>(HttpClientExtensions.JsonOptions, CancellationToken)
            ?? throw new InvalidOperationException("The share link is missing.");
    }
}
