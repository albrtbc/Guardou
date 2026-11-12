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

    [GeneratedRegex(@"^SCAN i\d*$")]
    private static partial Regex TableScan();
}
