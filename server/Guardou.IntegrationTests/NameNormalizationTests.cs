using System.Net;
using System.Net.Http.Json;
using Guardou.Application.Files;
using Guardou.Domain.Files;
using Guardou.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Guardou.IntegrationTests;

public sealed class NameNormalizationTests : SignedInTests
{
    private const string Composed = "relat\u00f3rio";
    private const string Decomposed = "relato\u0301rio";

    [Fact]
    public async Task StoresNamesInNfc()
    {
        ItemEntry folder = await Client.CreateFolderEntryAsync(ItemsApi.Root, Decomposed);
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, Decomposed + ".txt", "a");
        HttpResponseMessage duplicate = await Client.CreateFolderAsync(ItemsApi.Root, Composed);
        HttpResponseMessage renamed = await Client.PatchAsJsonAsync($"/api/items/{file.Id}", new { name = "nota\u0303o.txt" }, CancellationToken);

        Assert.Equal(Composed, folder.Name);
        Assert.Equal(Composed + ".txt", file.Name);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("not\u00e3o.txt", (await ItemsApi.ReadEntryAsync(renamed)).Name);
    }

    [Fact]
    public async Task SearchesWithADecomposedQuery()
    {
        await Client.CreateFolderEntryAsync(ItemsApi.Root, Composed);

        IReadOnlyList<ItemEntry>? results = await Client.GetFromJsonAsync<IReadOnlyList<ItemEntry>>(
            $"/api/search?query={Uri.EscapeDataString(Decomposed)}",
            HttpClientExtensions.JsonOptions,
            CancellationToken);

        Assert.Equal([Composed], results?.Select(entry => entry.Name));
    }

    [Fact]
    public async Task NormalizesTheStoredNamesWithASuffixForEqualSiblings()
    {
        ItemEntry docs = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        await Client.CreateFolderEntryAsync(docs.Id, Composed);
        ItemEntry stored = await Client.CreateFolderEntryAsync(docs.Id, "a");
        ItemEntry other = await Client.CreateFolderEntryAsync(docs.Id, "b");
        await RenameInDatabaseAsync(stored.Id, Decomposed);
        await RenameInDatabaseAsync(other.Id, "nota\u0303o");

        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ItemNameNormalizer>().NormalizeAsync(CancellationToken);
        }

        Assert.Equal(["not\u00e3o", Composed, Composed + " (1)"], (await Client.GetItemAsync(docs.Id)).Names().Order(StringComparer.Ordinal));
    }

    private async Task RenameInDatabaseAsync(string id, string name)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Guid itemId = OwnedItems.ParseId(id);
        await context.Set<Item>().Where(item => item.Id == itemId).ExecuteUpdateAsync(
            setters => setters.SetProperty(item => item.Name, name),
            CancellationToken);
    }
}
