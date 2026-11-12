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

    public async Task<IReadOnlyList<Item>> AncestryAsync(string ownerId, Guid id, CancellationToken cancellationToken)
    {
        await Pass();
        List<Item> chain = [];
        HashSet<Guid> seen = [];
        Guid? next = id;
        while (next is { } current && seen.Add(current) && _items.GetValueOrDefault(current) is { } item && item.OwnerId == ownerId)
        {
            chain.Add(item);
            next = item.ParentId;
        }

        return chain;
    }

    public async Task<IReadOnlyList<Item>> SubtreeAsync(Guid rootId, CancellationToken cancellationToken)
    {
        await Pass();
        return Collect(rootId, includeTrashed: true);
    }

    public async Task<IReadOnlyList<Item>> ActiveSubtreeAsync(Guid rootId, CancellationToken cancellationToken)
    {
        await Pass();
        return Collect(rootId, includeTrashed: false);
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

    private List<Item> Collect(Guid rootId, bool includeTrashed)
    {
        List<Item> found = [];
        if (!_items.TryGetValue(rootId, out Item? root))
        {
            return found;
        }

        Queue<Item> queue = [];
        queue.Enqueue(root);
        HashSet<Guid> seen = [rootId];
        while (queue.TryDequeue(out Item? current))
        {
            found.Add(current);
            foreach (Item child in _items.Values.Where(item => item.ParentId == current.Id && (includeTrashed || item.Trashed is null)))
            {
                if (seen.Add(child.Id))
                {
                    queue.Enqueue(child);
                }
            }
        }

        return found;
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
