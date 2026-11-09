using Guardou.Application.Files;
using Guardou.Domain.Files;
using Microsoft.Extensions.Logging;

namespace Guardou.UnitTests.Files;

public sealed class OwnedItemsTests
{
    private const string Owner = "owner";

    private readonly FakeItemStore _store = new();
    private readonly RecordingLogger<OwnedItems> _logger = new();
    private readonly TreeBuilder _tree;
    private readonly OwnedItems _items;

    public OwnedItemsTests()
    {
        _tree = new TreeBuilder(_store, Owner);
        _items = new OwnedItems(_store, _logger);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact(Timeout = 10_000)]
    public async Task FindsTheAncestorsOfANestedItem()
    {
        Item deepest = _tree.Chain(_tree.Root, 10);

        ItemPath? path = await _items.TryFindActiveAsync(Owner, deepest.Id, CancellationToken);

        Assert.NotNull(path);
        Assert.Equal(10, path.Ancestors.Count);
        Assert.Equal(_tree.Root.Id, path.Ancestors[0].Id);
        Assert.Empty(_logger.Entries);
    }

    [Fact(Timeout = 10_000)]
    public async Task TreatsAParentCycleAsNotFound()
    {
        Item first = _tree.Folder(_tree.Root, "a");
        Item second = _tree.Folder(first, "b");
        first.MoveTo(second, "a");

        ItemPath? path = await _items.TryFindActiveAsync(Owner, first.Id, CancellationToken);

        Assert.Null(path);
        (LogLevel level, string message) = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Error, level);
        Assert.Contains(first.Id.ToString(), message, StringComparison.Ordinal);
    }

    [Fact(Timeout = 10_000)]
    public async Task TreatsAnItemInsideACycleAsNotFound()
    {
        Item first = _tree.Folder(_tree.Root, "a");
        Item second = _tree.Folder(first, "b");
        first.MoveTo(second, "a");
        Item inside = _tree.Folder(second, "c");

        Assert.Null(await _items.TryFindActiveAsync(Owner, inside.Id, CancellationToken));
        Assert.Single(_logger.Entries);
    }

    [Fact(Timeout = 10_000)]
    public async Task TreatsAnItemThatIsItsOwnParentAsNotFound()
    {
        Item folder = _tree.Folder(_tree.Root, "a");
        folder.MoveTo(folder, "a");

        Assert.Null(await _items.TryFindActiveAsync(Owner, folder.Id, CancellationToken));
    }

    [Fact(Timeout = 10_000)]
    public async Task FindsItemsUpToTheWalkLimitAndNoDeeper()
    {
        Item atLimit = _tree.Chain(_tree.Root, TreeLimits.MaximumWalkDepth);
        Item beyond = _tree.Folder(atLimit, "beyond");

        ItemPath? found = await _items.TryFindActiveAsync(Owner, atLimit.Id, CancellationToken);
        ItemPath? missing = await _items.TryFindActiveAsync(Owner, beyond.Id, CancellationToken);

        Assert.NotNull(found);
        Assert.Null(missing);
        Assert.Single(_logger.Entries);
    }
}
