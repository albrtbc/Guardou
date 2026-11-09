using System.Net;
using System.Net.Http.Json;
using Guardou.Application.Files;
using Guardou.Domain.Files;
using Guardou.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Guardou.IntegrationTests;

public sealed class FolderTreeTests : SignedInTests
{
    private const int Cap = TreeLimits.MaximumFolderDepth;

    private readonly ItemStoreProbe _probe = new();

    [Fact]
    public async Task ConcurrentMovesCannotCreateACycle()
    {
        ItemEntry first = await Client.CreateFolderEntryAsync(ItemsApi.Root, "first");
        ItemEntry second = await Client.CreateFolderEntryAsync(ItemsApi.Root, "second");
        _probe.WaitForTwoSaves = true;

        Task<HttpResponseMessage> intoSecond = Client.TransferAsync("move", first.Id, second.Id);
        Task<HttpResponseMessage> intoFirst = Client.TransferAsync("move", second.Id, first.Id);
        HttpStatusCode[] statuses = (await Task.WhenAll(intoSecond, intoFirst)).Select(response => response.StatusCode).Order().ToArray();

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.BadRequest], statuses);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/items/{first.Id}", CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/items/{second.Id}", CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task AFolderLoopInTheDatabaseIsNotFoundInsteadOfHanging()
    {
        ItemEntry outer = await Client.CreateFolderEntryAsync(ItemsApi.Root, "outer");
        ItemEntry inner = await Client.CreateFolderEntryAsync(outer.Id, "inner");
        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Item outerItem = await context.Set<Item>().SingleAsync(item => item.Id == IdOf(outer), CancellationToken);
            Item innerItem = await context.Set<Item>().SingleAsync(item => item.Id == IdOf(inner), CancellationToken);
            outerItem.MoveTo(innerItem, "outer");
            await context.SaveChangesAsync(CancellationToken);
        }

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        HttpResponseMessage details = await Client.GetAsync($"/api/items/{outer.Id}", timeout.Token);
        HttpResponseMessage archive = await Client.GetAsync($"/api/items/{inner.Id}/download", timeout.Token);

        Assert.Equal(HttpStatusCode.NotFound, details.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, archive.StatusCode);
    }

    [Fact(Timeout = 60_000)]
    public async Task DeletingATreeDeeperThanTheWalkLimitRemovesEverything()
    {
        ItemEntry top = await Client.CreateFolderEntryAsync(ItemsApi.Root, "top");
        ItemEntry file = await Client.UploadEntryAsync(top.Id, "deep.txt", "12345");
        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Item parent = await context.Set<Item>().SingleAsync(item => item.Id == IdOf(top), CancellationToken);
            for (int level = 0; level < 300; level++)
            {
                Item folder = Item.CreateFolder(parent, $"level-{level}", parent.OwnerId, DateTimeOffset.UnixEpoch);
                context.Set<Item>().Add(folder);
                parent = folder;
            }

            Item fileItem = await context.Set<Item>().SingleAsync(item => item.Id == IdOf(file), CancellationToken);
            fileItem.MoveTo(parent, fileItem.Name);
            await context.SaveChangesAsync(CancellationToken);
        }

        Assert.Equal(5, (await UsageAsync()).UsedBytes);
        Assert.Single(BlobFiles());
        (await Client.PostSelectionAsync("/api/items/trash", top.Id)).EnsureSuccessStatusCode();
        (await Client.PostSelectionAsync("/api/trash/delete", top.Id)).EnsureSuccessStatusCode();

        Assert.Empty(BlobFiles());
        Assert.Equal(0, (await UsageAsync()).UsedBytes);
        await using AsyncServiceScope check = Factory.Services.CreateAsyncScope();
        Assert.Equal(0, await check.ServiceProvider.GetRequiredService<AppDbContext>().Set<Item>().CountAsync(item => item.ParentId != null, CancellationToken));
    }

    [Fact(Timeout = 60_000)]
    public async Task IndexingAFolderLoopDoesNotHang()
    {
        ItemEntry first = await Client.CreateFolderEntryAsync(ItemsApi.Root, "first");
        ItemEntry second = await Client.CreateFolderEntryAsync(ItemsApi.Root, "second");
        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Item firstItem = await context.Set<Item>().SingleAsync(item => item.Id == IdOf(first), CancellationToken);
            Item secondItem = await context.Set<Item>().SingleAsync(item => item.Id == IdOf(second), CancellationToken);
            secondItem.MoveTo(firstItem, "second");
            await context.SaveChangesAsync(CancellationToken);
        }

        await using AsyncServiceScope looping = Factory.Services.CreateAsyncScope();
        IItemStore store = looping.ServiceProvider.GetRequiredService<IItemStore>();
        Item? firstLoaded = await store.FindAnyAsync(IdOf(first), CancellationToken);
        Item? secondLoaded = await store.FindAnyAsync(IdOf(second), CancellationToken);
        firstLoaded?.MoveTo(secondLoaded ?? throw new InvalidOperationException("The folder is missing."), "first");

        await store.SaveAsync(CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/items/{first.Id}", CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task CreatingFoldersStopsAtTheDepthCap()
    {
        ItemEntry deepest = (await ChainAsync(ItemsApi.Root, Cap))[^1];

        HttpResponseMessage refused = await Client.CreateFolderAsync(deepest.Id, "too-deep");

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        ProblemDetails problem = await refused.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken) ?? new ProblemDetails();
        Assert.Contains("64", problem.Detail, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Created, (await Client.UploadAsync(deepest.Id, "file.txt", "a")).StatusCode);
    }

    [Fact]
    public async Task MovesCountTheDepthOfTheMovedSubtree()
    {
        List<ItemEntry> target = await ChainAsync(ItemsApi.Root, 40);
        ItemEntry tallTop = (await ChainAsync(ItemsApi.Root, 25, "tall"))[0];

        HttpResponseMessage refused = await Client.TransferAsync("move", tallTop.Id, target[^1].Id);
        HttpResponseMessage accepted = await Client.TransferAsync("move", tallTop.Id, target[^2].Id);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task MovesThatDoNotDeepenTheItemSkipTheSubtreeWalk()
    {
        List<ItemEntry> deep = await ChainAsync(ItemsApi.Root, 10);
        ItemEntry sideways = await Client.CreateFolderEntryAsync(ItemsApi.Root, "sideways");
        ItemEntry other = await Client.CreateFolderEntryAsync(ItemsApi.Root, "other");
        ItemEntry below = (await ChainAsync(sideways.Id, 3, "below"))[0];
        ItemEntry moved = await Client.CreateFolderEntryAsync(deep[^1].Id, "moved");
        await ChainAsync(moved.Id, 3, "inside");

        HttpResponseMessage shallower = await Client.TransferAsync("move", moved.Id, deep[4].Id);
        HttpResponseMessage level = await Client.TransferAsync("move", below.Id, other.Id);
        int walks = _probe.Count(nameof(IItemStore.ChildFolderIdsAsync));
        HttpResponseMessage deeper = await Client.TransferAsync("move", sideways.Id, deep[4].Id);

        Assert.Equal(HttpStatusCode.OK, shallower.StatusCode);
        Assert.Equal(HttpStatusCode.OK, level.StatusCode);
        Assert.Equal(0, walks);
        Assert.Equal(HttpStatusCode.OK, deeper.StatusCode);
        Assert.True(_probe.Count(nameof(IItemStore.ChildFolderIdsAsync)) <= TreeLimits.MaximumFolderDepth);
        Assert.True(_probe.Count(nameof(IItemStore.ChildFolderIdsAsync)) > walks);
    }

    [Fact]
    public async Task MovesDoNotWalkMoreLevelsThanTheCapAllows()
    {
        List<ItemEntry> target = await ChainAsync(ItemsApi.Root, Cap - 1);
        ItemEntry tallTop = (await ChainAsync(ItemsApi.Root, 30, "tall"))[0];
        int before = _probe.Count(nameof(IItemStore.ChildFolderIdsAsync));

        HttpResponseMessage refused = await Client.TransferAsync("move", tallTop.Id, target[^1].Id);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(1, _probe.Count(nameof(IItemStore.ChildFolderIdsAsync)) - before);
    }

    [Fact]
    public async Task CopiesCountTheDepthOfTheCopiedSubtree()
    {
        List<ItemEntry> target = await ChainAsync(ItemsApi.Root, 40);
        ItemEntry tallTop = (await ChainAsync(ItemsApi.Root, 25, "tall"))[0];

        HttpResponseMessage refused = await Client.TransferAsync("copy", tallTop.Id, target[^1].Id);
        HttpResponseMessage accepted = await Client.TransferAsync("copy", tallTop.Id, target[^2].Id);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task UploadPathsCountFromTheDestinationDepth()
    {
        ItemEntry destination = (await ChainAsync(ItemsApi.Root, Cap - 4))[^1];

        HttpResponseMessage refused = await Client.StartUploadAsync(destination.Id, "file.txt", 1, relativePath: "a/b/c/d/e/file.txt");
        HttpResponseMessage accepted = await Client.StartUploadAsync(destination.Id, "file.txt", 1, relativePath: "a/b/c/d/file.txt");

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    private protected override TestServerFactory CreateFactory()
    {
        return new TestServerFactory
        {
            TestServices = services =>
            {
                services.AddSingleton(_probe);
                services.Replace(ServiceDescriptor.Scoped(provider => ProbedItemStore.Wrap(
                    ActivatorUtilities.CreateInstance<EfItemStore>(provider),
                    provider.GetRequiredService<ItemStoreProbe>())));
            },
        };
    }

    private async Task<StorageUsage> UsageAsync()
    {
        return await Client.GetFromJsonAsync<StorageUsage>("/api/items/usage", HttpClientExtensions.JsonOptions, CancellationToken)
            ?? throw new InvalidOperationException("The usage is missing.");
    }

    private static Guid IdOf(ItemEntry entry)
    {
        return ItemId.TryParse(entry.Id, out Guid id) ? id : throw new InvalidOperationException("The id is not valid.");
    }

    private async Task<List<ItemEntry>> ChainAsync(string parentId, int length, string prefix = "level")
    {
        List<ItemEntry> chain = [];
        string current = parentId;
        for (int level = 0; level < length; level++)
        {
            ItemEntry folder = await Client.CreateFolderEntryAsync(current, $"{prefix}-{level}");
            chain.Add(folder);
            current = folder.Id;
        }

        return chain;
    }
}
