using Guardou.Application.Files;
using Guardou.Domain.Files;
using Microsoft.Extensions.Logging;

namespace Guardou.UnitTests.Files;

public sealed class ItemTreeTests
{
    private const string Owner = "owner";

    private readonly FakeItemStore _store = new();
    private readonly RecordingLogger<ItemTree> _logger = new();
    private readonly TreeBuilder _tree;
    private readonly ItemTree _walker;

    public ItemTreeTests()
    {
        _tree = new TreeBuilder(_store, Owner);
        _walker = new ItemTree(_store, _logger);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact(Timeout = 10_000)]
    public async Task WalksANestedTreeWithPathsAndDepths()
    {
        Item docs = _tree.Folder(_tree.Root, "docs");
        Item inner = _tree.Folder(docs, "inner");

        IReadOnlyList<TreeNode> nodes = await _walker.ActiveSubtreeAsync(docs, "docs", CancellationToken);

        Assert.Equal(["docs", "docs/inner"], nodes.Select(node => node.Path));
        Assert.Equal([0, 1], nodes.Select(node => node.Depth));
        Assert.Equal(inner.Id, nodes[1].Item.Id);
        Assert.Empty(_logger.Entries);
    }

    [Fact(Timeout = 10_000)]
    public async Task StopsAtAChildCycleAndLogsOnce()
    {
        Item first = _tree.Folder(_tree.Root, "a");
        Item second = _tree.Folder(first, "b");
        first.MoveTo(second, "a");

        IReadOnlyList<Item> items = await _walker.SubtreeAsync(first, CancellationToken);
        IReadOnlyList<TreeNode> active = await _walker.ActiveSubtreeAsync(first, "a", CancellationToken);

        Assert.Equal([first.Id, second.Id], items.Select(item => item.Id));
        Assert.Equal(2, active.Count);
        Assert.Equal(2, _logger.Entries.Count);
        Assert.All(_logger.Entries, entry => Assert.Equal(LogLevel.Error, entry.Level));
    }

    [Fact(Timeout = 10_000)]
    public async Task StopsActiveWalksAtTheWalkLimit()
    {
        Item top = _tree.Folder(_tree.Root, "top");
        _tree.Chain(top, TreeLimits.MaximumWalkDepth + 10);

        IReadOnlyList<TreeNode> nodes = await _walker.ActiveSubtreeAsync(top, "top", CancellationToken);

        Assert.Equal(TreeLimits.MaximumWalkDepth + 1, nodes.Count);
        Assert.Single(_logger.Entries);
    }

    [Fact(Timeout = 10_000)]
    public async Task RemovalWalksReachTreesDeeperThanTheWalkLimit()
    {
        Item top = _tree.Folder(_tree.Root, "top");
        _tree.Chain(top, 300);

        IReadOnlyList<Item> items = await _walker.SubtreeAsync(top, CancellationToken);

        Assert.Equal(301, items.Count);
        Assert.Empty(_logger.Entries);
    }

    [Fact(Timeout = 10_000)]
    public async Task StopsCollectingPastTheLimit()
    {
        Item docs = _tree.Folder(_tree.Root, "docs");
        for (int index = 0; index < 20; index++)
        {
            _tree.Folder(_tree.Folder(docs, $"folder-{index}"), "child");
        }

        IReadOnlyList<TreeNode> limited = await _walker.ActiveSubtreeAsync(docs, "docs", limit: 5, CancellationToken);
        IReadOnlyList<TreeNode> full = await _walker.ActiveSubtreeAsync(docs, "docs", CancellationToken);

        Assert.Equal(21, limited.Count);
        Assert.Equal(41, full.Count);
    }

    [Fact(Timeout = 10_000)]
    public async Task MeasuresTheHeightOneLevelAtATime()
    {
        Item docs = _tree.Folder(_tree.Root, "docs");
        Item empty = _tree.Folder(_tree.Root, "empty");
        _tree.Chain(docs, 3);

        Assert.Equal(3, await _walker.FolderHeightAsync(docs, maxLevels: 10, CancellationToken));
        Assert.Equal(0, await _walker.FolderHeightAsync(empty, maxLevels: 10, CancellationToken));
        Assert.Equal(5, _store.ChildFolderQueries);
    }

    [Fact(Timeout = 10_000)]
    public async Task StopsMeasuringAfterTheAllowedLevels()
    {
        Item top = _tree.Folder(_tree.Root, "top");
        _tree.Chain(top, 100);

        int height = await _walker.FolderHeightAsync(top, maxLevels: 5, CancellationToken);

        Assert.Equal(5, height);
        Assert.Equal(5, _store.ChildFolderQueries);
    }

    [Fact(Timeout = 10_000)]
    public async Task MeasuresFilesWithoutQueries()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        Item file = Item.CreateFile(_tree.Root, new StoredFile(Guid.CreateVersion7(), "a.txt", 1, "key", now), Owner, now);

        Assert.Equal(-1, await _walker.FolderHeightAsync(file, maxLevels: TreeLimits.MaximumFolderDepth, CancellationToken));
        Assert.Equal(0, _store.ChildFolderQueries);
    }

    [Fact(Timeout = 10_000)]
    public async Task MeasuresNothingWhenNoLevelsAreAllowed()
    {
        Item folder = _tree.Folder(_tree.Root, "docs");
        _tree.Folder(folder, "inner");

        Assert.Equal(0, await _walker.FolderHeightAsync(folder, maxLevels: 0, CancellationToken));
        Assert.Equal(0, _store.ChildFolderQueries);
    }

    [Fact(Timeout = 10_000)]
    public async Task HeightMeasuringSurvivesAFolderLoop()
    {
        Item first = _tree.Folder(_tree.Root, "a");
        Item second = _tree.Folder(first, "b");
        first.MoveTo(second, "a");

        int height = await _walker.FolderHeightAsync(first, maxLevels: TreeLimits.MaximumFolderDepth, CancellationToken);

        Assert.Equal(1, height);
        Assert.True(_store.ChildFolderQueries <= 3);
    }
}
