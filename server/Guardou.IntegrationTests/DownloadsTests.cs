using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Guardou.Application.Files;
using Guardou.Application.Shares;
using Microsoft.Extensions.DependencyInjection;

namespace Guardou.IntegrationTests;

public sealed class DownloadsTests : SignedInTests
{
    [Fact]
    public async Task DownloadsAFileWithItsName()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "report.txt", "0123456789");

        HttpResponseMessage response = await Client.GetAsync($"/api/items/{file.Id}/download", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("report.txt", response.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal("0123456789", await response.Content.ReadAsStringAsync(CancellationToken));
    }

    [Fact]
    public async Task DownloadsARange()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "report.txt", "0123456789");
        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/items/{file.Id}/download");
        request.Headers.Range = new RangeHeaderValue(2, 5);

        HttpResponseMessage response = await Client.SendAsync(request, CancellationToken);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal("2345", await response.Content.ReadAsStringAsync(CancellationToken));
    }

    [Fact]
    public async Task ViewsAFileInline()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "page.html", "<p>hi</p>");

        HttpResponseMessage response = await Client.GetAsync($"/api/items/{file.Id}/view", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("inline", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("sandbox", Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Fact]
    public async Task ViewsAPdfWithoutTheSandbox()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "manual.pdf", "%PDF-1.4");

        HttpResponseMessage response = await Client.GetAsync($"/api/items/{file.Id}/view", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Fact]
    public async Task DownloadsAFolderAsAZip()
    {
        ItemEntry docs = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        await Client.CreateFolderEntryAsync(docs.Id, "empty");
        await Client.UploadEntryAsync(docs.Id, "a.txt", "a");

        HttpResponseMessage response = await Client.GetAsync($"/api/items/{docs.Id}/download", CancellationToken);

        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("docs.zip", response.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal(["docs/", "docs/a.txt", "docs/empty/"], await EntriesAsync(response));
    }

    [Fact]
    public async Task DownloadsASelectionAsAZip()
    {
        ItemEntry first = await Client.UploadEntryAsync(ItemsApi.Root, "a.txt", "a");
        ItemEntry second = await Client.UploadEntryAsync(ItemsApi.Root, "b.txt", "b");

        HttpResponseMessage response = await Client.GetAsync($"/api/items/archive?ids={first.Id}&ids={second.Id}", CancellationToken);
        HttpResponseMessage empty = await Client.GetAsync("/api/items/archive", CancellationToken);

        Assert.Equal("download.zip", response.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal(["a.txt", "b.txt"], await EntriesAsync(response));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task NumbersZipNamesThatAreEqualWithoutCase()
    {
        ItemEntry docs = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        await Client.UploadEntryAsync(docs.Id, "Report.txt", "a");
        await Client.UploadEntryAsync(docs.Id, "report.txt", "b");
        ItemEntry folder = await Client.CreateFolderEntryAsync(ItemsApi.Root, "Docs");

        HttpResponseMessage single = await Client.GetAsync($"/api/items/{docs.Id}/download", CancellationToken);
        HttpResponseMessage selection = await Client.GetAsync($"/api/items/archive?ids={docs.Id}&ids={folder.Id}", CancellationToken);

        Assert.Equal(["docs/", "docs/Report.txt", "docs/report (1).txt"], await EntriesAsync(single));
        Assert.Equal(["Docs (1)/", "docs/", "docs/Report.txt", "docs/report (1).txt"], await EntriesAsync(selection));
    }

    [Fact]
    public async Task ArchivesEachSelectedItemOnce()
    {
        ItemEntry docs = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        ItemEntry inner = await Client.CreateFolderEntryAsync(docs.Id, "inner");
        ItemEntry inside = await Client.UploadEntryAsync(inner.Id, "a.txt", "a");
        ItemEntry other = await Client.UploadEntryAsync(ItemsApi.Root, "other.txt", "o");
        string query = string.Join('&', new[] { docs.Id, docs.Id, inside.Id, other.Id, other.Id, inner.Id }.Select(id => $"ids={id}"));

        HttpResponseMessage response = await Client.GetAsync($"/api/items/archive?{query}", CancellationToken);

        Assert.Equal(["docs/", "docs/inner/", "docs/inner/a.txt", "other.txt"], await EntriesAsync(response));
    }

    [Fact]
    public async Task NamesTheArchiveAfterTheSingleFolderLeftBySelectionCleanup()
    {
        ItemEntry docs = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        ItemEntry file = await Client.UploadEntryAsync(docs.Id, "a.txt", "a");

        HttpResponseMessage response = await Client.GetAsync($"/api/items/archive?ids={docs.Id}&ids={file.Id}", CancellationToken);

        Assert.Equal("docs.zip", response.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal(["docs/", "docs/a.txt"], await EntriesAsync(response));
    }

    [Fact]
    public async Task RefusesArchivesWithoutWaitingWhenAllSlotsAreTaken()
    {
        ItemEntry docs = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        await Client.UploadEntryAsync(docs.Id, "a.txt", "a");
        string token = await ShareTokenAsync(docs.Id);
        using HttpClient anonymous = Factory.CreateClient();
        using ArchiveSlotsTaken taken = new(Factory, ArchiveAudience.SignedIn, ArchiveSlots.SignedInCapacity);

        HttpResponseMessage folder = await Client.GetAsync($"/api/items/{docs.Id}/download", CancellationToken);
        HttpResponseMessage selection = await Client.GetAsync($"/api/items/archive?ids={docs.Id}", CancellationToken);
        HttpResponseMessage shared = await anonymous.GetAsync($"/api/shares/{token}/items/{docs.Id}/download", CancellationToken);
        taken.ReleaseOne();
        HttpResponseMessage retried = await Client.GetAsync($"/api/items/{docs.Id}/download", CancellationToken);

        Assert.All([folder, selection], AssertBusy);
        Assert.Equal(HttpStatusCode.OK, shared.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
    }

    [Fact]
    public async Task ShareLinkArchivesHaveTheirOwnSmallerPool()
    {
        ItemEntry docs = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        await Client.UploadEntryAsync(docs.Id, "a.txt", "a");
        string token = await ShareTokenAsync(docs.Id);
        using HttpClient anonymous = Factory.CreateClient();
        using ArchiveSlotsTaken taken = new(Factory, ArchiveAudience.Anonymous, ArchiveSlots.AnonymousCapacity);

        HttpResponseMessage shared = await anonymous.GetAsync($"/api/shares/{token}/items/{docs.Id}/download", CancellationToken);
        HttpResponseMessage owner = await Client.GetAsync($"/api/items/{docs.Id}/download", CancellationToken);
        taken.ReleaseOne();
        HttpResponseMessage retried = await anonymous.GetAsync($"/api/shares/{token}/items/{docs.Id}/download", CancellationToken);

        AssertBusy(shared);
        Assert.Equal(HttpStatusCode.OK, owner.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
    }

    [Fact]
    public async Task ReleasesTheSlotAfterADownloadAndAfterAFailure()
    {
        ItemEntry docs = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        await Client.UploadEntryAsync(docs.Id, "a.txt", "a");

        for (int round = 0; round < ArchiveSlots.SignedInCapacity + 1; round++)
        {
            HttpResponseMessage response = await Client.GetAsync($"/api/items/{docs.Id}/download", CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await response.Content.ReadAsByteArrayAsync(CancellationToken);
        }

        foreach (string blob in Directory.EnumerateFiles(Factory.FilesDirectory, "*", SearchOption.AllDirectories))
        {
            File.Delete(blob);
        }

        await Assert.ThrowsAsync<HttpRequestException>(() => Client.GetAsync($"/api/items/{docs.Id}/download", CancellationToken));

        using ArchiveSlotsTaken all = new(Factory, ArchiveAudience.SignedIn, ArchiveSlots.SignedInCapacity);
    }

    private static void AssertBusy(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(ArchiveBusyException.RetryAfterSeconds), response.Headers.RetryAfter?.Delta);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private async Task<string> ShareTokenAsync(string id)
    {
        HttpResponseMessage response = await Client.PutAsync($"/api/items/{id}/share", null, CancellationToken);
        response.EnsureSuccessStatusCode();
        string url = (await response.Content.ReadFromJsonAsync<ShareLink>(HttpClientExtensions.JsonOptions, CancellationToken))?.Url ?? string.Empty;
        return url[(url.LastIndexOf('/') + 1)..];
    }

    private sealed class ArchiveSlotsTaken : IDisposable
    {
        private readonly List<IDisposable> _slots;

        public ArchiveSlotsTaken(TestServerFactory factory, ArchiveAudience audience, int count)
        {
            ArchiveSlots slots = factory.Services.GetRequiredService<ArchiveSlots>();
            _slots = Enumerable.Range(0, count).Select(_ => slots.Acquire(audience)).ToList();
        }

        public void ReleaseOne()
        {
            _slots[0].Dispose();
            _slots.RemoveAt(0);
        }

        public void Dispose()
        {
            _slots.ForEach(slot => slot.Dispose());
        }
    }

    private static async Task<IReadOnlyList<string>> EntriesAsync(HttpResponseMessage response)
    {
        await using Stream content = await response.Content.ReadAsStreamAsync(CancellationToken);
        using MemoryStream buffer = new();
        await content.CopyToAsync(buffer, CancellationToken);
        await using ZipArchive zip = new(buffer, ZipArchiveMode.Read);
        return zip.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal).ToList();
    }
}
