using System.Net;
using System.Net.Http.Json;
using Guardou.Application.Files;
using Guardou.Application.Shares;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Guardou.IntegrationTests;

public sealed class ArchiveLimitsTests : SignedInTests
{
    private const int MaximumEntries = 3;
    private const long MaximumBytes = 10;

    [Fact]
    public async Task RefusesFoldersWithTooManyEntries()
    {
        ItemEntry full = await FolderWithFilesAsync("full", "a", "b");
        ItemEntry over = await FolderWithFilesAsync("over", "a", "b", "c");

        HttpResponseMessage accepted = await Client.GetAsync($"/api/items/{full.Id}/download", CancellationToken);
        HttpResponseMessage refused = await Client.GetAsync($"/api/items/{over.Id}/download", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        await AssertTooLargeAsync(refused);
    }

    [Fact]
    public async Task CountsTheEntriesOfAWholeSelection()
    {
        ItemEntry first = await FolderWithFilesAsync("first", "a");
        ItemEntry second = await FolderWithFilesAsync("second", "a");

        HttpResponseMessage accepted = await Client.GetAsync($"/api/items/archive?ids={first.Id}", CancellationToken);
        HttpResponseMessage refused = await Client.GetAsync($"/api/items/archive?ids={first.Id}&ids={second.Id}", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        await AssertTooLargeAsync(refused);
    }

    [Fact]
    public async Task RefusesSelectionsWithTooManyBytes()
    {
        ItemEntry exact = await Client.CreateFolderEntryAsync(ItemsApi.Root, "exact");
        await Client.UploadEntryAsync(exact.Id, "a.txt", new string('a', (int)MaximumBytes));
        ItemEntry over = await Client.CreateFolderEntryAsync(ItemsApi.Root, "over");
        await Client.UploadEntryAsync(over.Id, "a.txt", new string('a', (int)MaximumBytes + 1));

        HttpResponseMessage accepted = await Client.GetAsync($"/api/items/{exact.Id}/download", CancellationToken);
        HttpResponseMessage refused = await Client.GetAsync($"/api/items/{over.Id}/download", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        await AssertTooLargeAsync(refused);
    }

    [Fact]
    public async Task RefusesSharedFoldersThatAreTooLarge()
    {
        ItemEntry over = await FolderWithFilesAsync("over", "a", "b", "c");
        HttpResponseMessage shared = await Client.PutAsync($"/api/items/{over.Id}/share", null, CancellationToken);
        shared.EnsureSuccessStatusCode();
        string url = (await shared.Content.ReadFromJsonAsync<ShareLink>(HttpClientExtensions.JsonOptions, CancellationToken))?.Url ?? string.Empty;
        using HttpClient anonymous = Factory.CreateClient();

        HttpResponseMessage refused = await anonymous.GetAsync($"/api/shares/{url[(url.LastIndexOf('/') + 1)..]}/items/{over.Id}/download", CancellationToken);

        await AssertTooLargeAsync(refused);
    }

    private protected override TestServerFactory CreateFactory()
    {
        return new TestServerFactory
        {
            TestServices = services => services.Replace(ServiceDescriptor.Singleton(new ArchiveLimits(MaximumEntries, MaximumBytes))),
        };
    }

    private static async Task AssertTooLargeAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal([ArchiveTooLargeException.Code], await UsersApi.ReadErrorCodesAsync(response));
    }

    private async Task<ItemEntry> FolderWithFilesAsync(string name, params string[] files)
    {
        ItemEntry folder = await Client.CreateFolderEntryAsync(ItemsApi.Root, name);
        foreach (string file in files)
        {
            await Client.UploadEntryAsync(folder.Id, file + ".txt", "x");
        }

        return folder;
    }
}
