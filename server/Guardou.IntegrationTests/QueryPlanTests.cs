using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Guardou.Api.Models;
using Guardou.Application.Files;
using Guardou.Domain.Files;
using Guardou.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Guardou.IntegrationTests;

public sealed partial class QueryPlanTests : SignedInTests
{
    private readonly QueryPlanRecorder _recorder = new();

    [Fact]
    public async Task SubtreesUseTheParentIndex()
    {
        IReadOnlyList<string> plan = await PlanOfAsync(store => store.SubtreeAsync(Guid.NewGuid(), CancellationToken));

        Assert.Contains(plan, detail => detail.Contains("USING INDEX IX_Items_ParentId (ParentId=?)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadsTheAncestorsWithOneQuery()
    {
        string deepest = await CreateFoldersAsync("a", "b", "c", "d");
        string ownerId = await AdminIdAsync();
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        OwnedItems items = scope.ServiceProvider.GetRequiredService<OwnedItems>();
        _recorder.Clear();

        ItemPath? path = await items.TryFindActiveAsync(ownerId, OwnedItems.ParseId(deepest), CancellationToken);

        Assert.Single(_recorder.Plans);
        Assert.Equal(["", "a", "b", "c"], path?.Ancestors.Select(ancestor => ancestor.Name));
    }

    [Fact]
    public async Task LoadsTheSubtreeWithOneQuery()
    {
        ItemEntry top = await Client.CreateFolderEntryAsync(ItemsApi.Root, "top");
        ItemEntry inner = await Client.CreateFolderEntryAsync(top.Id, "inner");
        await Client.UploadEntryAsync(inner.Id, "a.txt", "a");
        ItemEntry trashed = await Client.UploadEntryAsync(inner.Id, "b.txt", "b");
        (await Client.PostSelectionAsync("/api/items/trash", trashed.Id)).EnsureSuccessStatusCode();
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IItemStore store = scope.ServiceProvider.GetRequiredService<IItemStore>();
        Item root = await store.FindAnyAsync(OwnedItems.ParseId(top.Id), CancellationToken) ?? throw new InvalidOperationException("The folder is missing.");
        _recorder.Clear();

        IReadOnlyList<TreeNode> nodes = await scope.ServiceProvider.GetRequiredService<ItemTree>().ActiveSubtreeAsync(root, "top", CancellationToken);

        Assert.Single(_recorder.Plans);
        Assert.Equal(["top", "top/inner", "top/inner/a.txt"], nodes.Select(node => node.Path));
    }

    [Fact]
    public async Task BoundsTheSubtreeInsideTheRecursiveQuery()
    {
        const int Limit = 10;
        const int Branches = 5;
        Guid rootId = await CreateWideTreeAsync(Branches, leavesPerBranch: 12);
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _recorder.Clear();

        List<Item> rows = await ItemTreeQueries.Subtree(context.Set<Item>(), rootId, includeTrashed: false, Limit).ToListAsync(CancellationToken);

        HashSet<Guid> returned = rows.Select(row => row.Id).ToHashSet();
        Assert.Equal(Limit, rows.Count);
        Assert.Contains(rootId, returned);
        Assert.All(rows.Where(row => row.Id != rootId), row => Assert.Contains(row.ParentId ?? Guid.Empty, returned));
        Assert.Equal(Branches, rows.Count(row => row.ParentId == rootId));
        // The plan is the same with the LIMIT outside the CTE (MATERIALIZE Tree), so the SQL text proves where it sits; on an outer LIMIT the rows above are not connected either.
        string statement = Assert.Single(_recorder.Statements);
        Assert.Matches(LimitInsideTheRecursiveQuery(), statement);
        Assert.Equal(1, Regex.Count(statement, "LIMIT", RegexOptions.IgnoreCase));
        IReadOnlyList<string> plan = Assert.Single(_recorder.Plans);
        Assert.Contains(plan, detail => detail.Contains("USING INDEX IX_Items_ParentId (ParentId=?)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadsOneRowPastTheArchiveLimit()
    {
        const int Limit = 10;
        Guid rootId = await CreateWideTreeAsync(branches: 5, leavesPerBranch: 12);
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        Item root = await scope.ServiceProvider.GetRequiredService<IItemStore>().FindAnyAsync(rootId, CancellationToken)
            ?? throw new InvalidOperationException("The folder is missing.");
        _recorder.Clear();

        IReadOnlyList<TreeNode> nodes = await scope.ServiceProvider.GetRequiredService<ItemTree>().ActiveSubtreeAsync(root, "wide", Limit, CancellationToken);

        Assert.Matches(LimitInsideTheRecursiveQuery(), Assert.Single(_recorder.Statements));
        Assert.Equal(Limit + 1, nodes.Count);
        Assert.Equal("wide", nodes[0].Path);
    }

    [Fact]
    public async Task ThePurgeUsesTheTrashIndex()
    {
        IReadOnlyList<string> plan = await PlanOfAsync(store => store.TrashedBeforeAsync(DateTimeOffset.UtcNow, CancellationToken));

        Assert.Contains(plan, detail => detail.Contains("USING INDEX IX_Items_Trashed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RecentItemsDoNotScanTheItemsOfAllOwners()
    {
        IReadOnlyList<string> plan = await PlanOfAsync(store => store.RecentAsync("user", ItemKind.File, 0, 20, CancellationToken));

        Assert.Contains(plan, detail => detail.Contains("USING INDEX IX_Items_CreatedBy_Kind (CreatedBy=? AND Kind=?)", StringComparison.Ordinal));
        Assert.DoesNotContain(plan, detail => TableScan().IsMatch(detail));
    }

    private async Task<string> CreateFoldersAsync(params string[] names)
    {
        string parent = ItemsApi.Root;
        foreach (string name in names)
        {
            parent = (await Client.CreateFolderEntryAsync(parent, name)).Id;
        }

        return parent;
    }

    private async Task<Guid> CreateWideTreeAsync(int branches, int leavesPerBranch)
    {
        ItemEntry top = await Client.CreateFolderEntryAsync(ItemsApi.Root, "wide");
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Guid rootId = OwnedItems.ParseId(top.Id);
        Item root = await context.Set<Item>().SingleAsync(item => item.Id == rootId, CancellationToken);
        for (int branch = 0; branch < branches; branch++)
        {
            Item folder = Item.CreateFolder(root, $"branch-{branch}", root.OwnerId, DateTimeOffset.UnixEpoch);
            context.Set<Item>().Add(folder);
            for (int leaf = 0; leaf < leavesPerBranch; leaf++)
            {
                context.Set<Item>().Add(Item.CreateFolder(folder, $"leaf-{leaf}", root.OwnerId, DateTimeOffset.UnixEpoch));
            }
        }

        await context.SaveChangesAsync(CancellationToken);
        return rootId;
    }

    private async Task<string> AdminIdAsync()
    {
        CurrentUser? me = await Client.GetFromJsonAsync<CurrentUser>("/api/auth/me", CancellationToken);
        return me?.Id ?? throw new InvalidOperationException("The user is missing.");
    }

    private async Task<IReadOnlyList<string>> PlanOfAsync(Func<IItemStore, Task> query)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IItemStore store = scope.ServiceProvider.GetRequiredService<IItemStore>();
        _recorder.Clear();
        await query(store);
        return Assert.Single(_recorder.Plans);
    }

    private protected override TestServerFactory CreateFactory()
    {
        return new TestServerFactory
        {
            TestServices = services => services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(_recorder)),
        };
    }

    [GeneratedRegex(@"LIMIT\s+@\w+\s*\)\s*SELECT", RegexOptions.IgnoreCase)]
    private static partial Regex LimitInsideTheRecursiveQuery();

    [GeneratedRegex(@"^SCAN i\d*$")]
    private static partial Regex TableScan();
}
