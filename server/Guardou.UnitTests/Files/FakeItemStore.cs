using Guardou.Application.Files;
using Guardou.Domain.Files;

namespace Guardou.UnitTests.Files;

internal sealed class FakeItemStore : IItemStore
{
    public const int CallLimit = 100_000;

    private readonly Dictionary<Guid, Item> _items = [];
    private int _calls;

    public void Add(Item item)
    {
        _items[item.Id] = item;
    }

    public async Task<Item?> FindAsync(string ownerId, Guid id, CancellationToken cancellationToken)
    {
        await Pass();
        return _items.GetValueOrDefault(id) is { } item && item.OwnerId == ownerId ? item : null;
    }

    public async Task<IReadOnlyList<Item>> ChildrenAsync(Guid parentId, CancellationToken cancellationToken)
    {
        await Pass();
        return _items.Values.Where(item => item.ParentId == parentId && item.Trashed is null).ToList();
    }

    public async Task<IReadOnlyList<Item>> ChildrenWithTrashAsync(Guid parentId, CancellationToken cancellationToken)
    {
        await Pass();
        return _items.Values.Where(item => item.ParentId == parentId).ToList();
    }

    public int ChildFolderQueries { get; private set; }

    public int Calls => _calls;

    public Item? ChildAlwaysFound { get; set; }

    public async Task<IReadOnlyList<Guid>> ChildFolderIdsAsync(IReadOnlyCollection<Guid> parentIds, CancellationToken cancellationToken)
    {
        await Pass();
        ChildFolderQueries++;
        return _items.Values
            .Where(item => item.IsFolder && item.ParentId is { } parentId && parentIds.Contains(parentId))
            .Select(item => item.Id)
            .ToList();
    }

    public async Task<Item?> FindChildAsync(Guid parentId, string name, CancellationToken cancellationToken)
    {
        await Pass();
        return ChildAlwaysFound ?? throw new NotSupportedException();
    }

    public Task<Item?> FindAnyAsync(Guid id, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<Item?> FindRootAsync(string ownerId, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<IReadOnlyList<Guid>> RootIdsAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<IReadOnlyList<Item>> StarredAsync(string ownerId, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<IReadOnlyList<Item>> TrashedAsync(string ownerId, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<IReadOnlyList<Item>> TrashedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<IReadOnlyList<Item>> ActiveFilesAsync(Guid? after, int limit, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<IReadOnlyList<Item>> RecentAsync(string userId, ItemKind kind, int skip, int take, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<IReadOnlyList<string>> StorageKeysAsync(string ownerId, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public void Remove(Item item)
    {
        _items.Remove(item.Id);
    }

    public Task SaveAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task Pass()
    {
        await Task.Yield();
        if (++_calls > CallLimit)
        {
            throw new InvalidOperationException("The walk did not stop.");
        }
    }
}
