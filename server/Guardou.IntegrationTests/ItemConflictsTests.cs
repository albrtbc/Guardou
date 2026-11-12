using System.Net;
using System.Net.Http.Json;
using Guardou.Application.Files;
using Guardou.Domain.Files;
using Microsoft.Extensions.DependencyInjection;

namespace Guardou.IntegrationTests;

public sealed class ItemConflictsTests : SignedInTests
{
    [Fact]
    public async Task ConcurrentFoldersWithTheSameNameGiveConflict()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            string name = $"docs {attempt}";

            HttpResponseMessage[] responses = await Task.WhenAll(
                Client.CreateFolderAsync(ItemsApi.Root, name),
                Client.CreateFolderAsync(ItemsApi.Root, name));

            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        }
    }

    [Fact]
    public async Task AChangeOfAStaleItemGivesItemChanged()
    {
        ItemEntry entry = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        Guid id = OwnedItems.ParseId(entry.Id);
        await using AsyncServiceScope first = Factory.Services.CreateAsyncScope();
        await using AsyncServiceScope second = Factory.Services.CreateAsyncScope();
        IItemStore firstStore = first.ServiceProvider.GetRequiredService<IItemStore>();
        IItemStore secondStore = second.ServiceProvider.GetRequiredService<IItemStore>();
        Item firstItem = await firstStore.FindAnyAsync(id, CancellationToken) ?? throw new InvalidOperationException("The item is missing.");
        Item secondItem = await secondStore.FindAnyAsync(id, CancellationToken) ?? throw new InvalidOperationException("The item is missing.");

        firstItem.Rename("first", DateTimeOffset.UtcNow);
        await firstStore.SaveAsync(CancellationToken);
        secondItem.Rename("second", DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<ItemChangedException>(() => secondStore.SaveAsync(CancellationToken));
        Assert.Equal("first", (await Client.GetItemAsync(entry.Id)).Entry.Name);
    }

    [Fact]
    public async Task ConcurrentRenamesDoNotFailTheServer()
    {
        ItemEntry entry = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");

        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(number =>
            Client.PatchAsJsonAsync($"/api/items/{entry.Id}", new { name = $"docs {number}" }, CancellationToken)));

        Assert.All(responses, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
    }
}
