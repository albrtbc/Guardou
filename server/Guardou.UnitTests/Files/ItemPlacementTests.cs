using Guardou.Application.Files;
using Guardou.Domain.Files;

namespace Guardou.UnitTests.Files;

public sealed class ItemPlacementTests
{
    private const string Owner = "owner";

    private readonly FakeItemStore _store = new();
    private readonly TreeBuilder _tree;
    private readonly ItemPlacement _placement;

    public ItemPlacementTests()
    {
        _tree = new TreeBuilder(_store, Owner);
        _placement = new ItemPlacement(_store, shares: null!, TimeProvider.System);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact(Timeout = 30_000)]
    public async Task StopsLookingForAFreeNameAfterTheCap()
    {
        _store.ChildAlwaysFound = _tree.Folder(_tree.Root, "taken");

        ItemConflictException error = await Assert.ThrowsAsync<ItemConflictException>(
            () => _placement.ResolveNameAsync(_tree.Root, "report.txt", ConflictPolicy.Rename, source: null, CancellationToken));

        Assert.Equal("report.txt", error.Name);
        Assert.Contains(ItemPlacement.MaximumNumberedNames.ToString(System.Globalization.CultureInfo.InvariantCulture), error.Message, StringComparison.Ordinal);
        Assert.Equal(ItemPlacement.MaximumNumberedNames + 1, _store.Calls);
    }
}
