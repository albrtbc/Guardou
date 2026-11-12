using Guardou.Application.Persistence;
using Guardou.Application.Previews;
using Guardou.Application.Quotas;
using Guardou.Domain.Files;
using Guardou.Domain.Tasks;

namespace Guardou.Application.Files;

public sealed class ItemOrganizer
{
    private readonly IItemStore _store;
    private readonly IBlobStore _blobs;
    private readonly OwnedItems _items;
    private readonly ItemPlacement _placement;
    private readonly ItemTree _tree;
    private readonly ItemEntryFactory _entries;
    private readonly PreviewTasks _previews;
    private readonly StorageReservations _reservations;
    private readonly ItemMoveLocks _moveLocks;
    private readonly IWriteTransactions _transactions;
    private readonly TimeProvider _time;

    internal ItemOrganizer(
        IItemStore store,
        IBlobStore blobs,
        OwnedItems items,
        ItemPlacement placement,
        ItemTree tree,
        ItemEntryFactory entries,
        PreviewTasks previews,
        StorageReservations reservations,
        ItemMoveLocks moveLocks,
        IWriteTransactions transactions,
        TimeProvider time)
    {
        _store = store;
        _blobs = blobs;
        _items = items;
        _placement = placement;
        _tree = tree;
        _entries = entries;
        _previews = previews;
        _reservations = reservations;
        _moveLocks = moveLocks;
        _transactions = transactions;
        _time = time;
    }

    public async Task<ItemEntry> CreateFolderAsync(string ownerId, string parentId, string name, CancellationToken cancellationToken)
    {
        Item folder;
        SemaphoreSlim ownerLock = _moveLocks.For(ownerId);
        await ownerLock.WaitAsync(cancellationToken);
        try
        {
            ItemPath parent = await _items.FindActiveFolderAsync(ownerId, parentId, cancellationToken);
            FolderDepth.EnsureWithin(parent.Ancestors.Count + 1);
            string validName = ItemPlacement.ValidName(name);
            await _placement.ResolveNameAsync(parent.Item, validName, ConflictPolicy.Fail, null, cancellationToken);
            folder = Item.CreateFolder(parent.Item, validName, ownerId, _time.GetUtcNow());
            _store.Add(folder);
            await _store.SaveAsync(cancellationToken);
        }
        finally
        {
            ownerLock.Release();
        }

        return await _entries.CreateAsync(folder, ownerId, cancellationToken);
    }

    public async Task<ItemEntry> RenameAsync(string ownerId, string id, string name, CancellationToken cancellationToken)
    {
        ItemPath path = await _items.FindModifiableAsync(ownerId, id, cancellationToken);
        Item item = path.Item;
        Item parent = path.Ancestors[^1];
        string validName = ItemPlacement.ValidName(name);
        if (item.Name != validName)
        {
            await _placement.ResolveNameAsync(parent, validName, ConflictPolicy.Fail, path, cancellationToken);
            item.Rename(validName, _time.GetUtcNow());
            await _store.SaveAsync(cancellationToken);
        }

        return await _entries.CreateAsync(item, ownerId, cancellationToken);
    }

    public async Task<ItemEntry?> MoveAsync(
        string ownerId,
        string id,
        string destinationId,
        ConflictPolicy conflict,
        CancellationToken cancellationToken)
    {
        Item? item;
        SemaphoreSlim ownerLock = _moveLocks.For(ownerId);
        await ownerLock.WaitAsync(cancellationToken);
        try
        {
            item = await _transactions.RunAsync(
                () => PlaceMovedAsync(ownerId, id, destinationId, conflict, cancellationToken),
                cancellationToken);
        }
        finally
        {
            ownerLock.Release();
        }

        return item is null ? null : await _entries.CreateAsync(item, ownerId, cancellationToken);
    }

    private async Task<Item?> PlaceMovedAsync(
        string ownerId,
        string id,
        string destinationId,
        ConflictPolicy conflict,
        CancellationToken cancellationToken)
    {
        ItemPath source = await _items.FindModifiableAsync(ownerId, id, cancellationToken);
        ItemPath destination = await _items.FindActiveFolderAsync(ownerId, destinationId, cancellationToken);
        Item item = source.Item;
        if (destination.Contains(item.Id))
        {
            throw new InvalidItemRequestException("A folder cannot move into itself.");
        }

        if (item.ParentId == destination.Item.Id)
        {
            return item;
        }

        int destinationDepth = destination.Ancestors.Count;
        if (destinationDepth + 1 > source.Ancestors.Count)
        {
            int height = await _tree.FolderHeightAsync(item, TreeLimits.MaximumFolderDepth - destinationDepth, cancellationToken);
            FolderDepth.EnsureWithin(destinationDepth + 1 + height);
        }

        string? name = await _placement.ResolveNameAsync(destination.Item, item.Name, conflict, source, cancellationToken);
        if (name is null)
        {
            return null;
        }

        item.MoveTo(destination.Item, name);
        await _store.SaveAsync(cancellationToken);
        return item;
    }

    public async Task<ItemEntry?> CopyAsync(
        string ownerId,
        string id,
        string destinationId,
        ConflictPolicy conflict,
        CancellationToken cancellationToken)
    {
        ItemPath source = await _items.FindModifiableAsync(ownerId, id, cancellationToken);
        ItemPath destination = await _items.FindActiveFolderAsync(ownerId, destinationId, cancellationToken);
        if (destination.Contains(source.Item.Id))
        {
            throw new InvalidItemRequestException("A folder cannot copy into itself.");
        }

        IReadOnlyList<TreeNode> nodes = await _tree.ActiveSubtreeAsync(source.Item, source.Item.Name, cancellationToken);
        FolderDepth.EnsureWithin(destination.Ancestors.Count + 1 + ItemTree.DeepestFolderLevel(nodes));
        long size = nodes.Sum(node => node.Item.Size);
        Item? copy = await _reservations.ReserveAsync(
            ownerId,
            size,
            () => PlaceCopyAsync(source, destination.Item, conflict, ownerId, cancellationToken),
            cancellationToken);
        return copy is null ? null : await _entries.CreateAsync(copy, ownerId, cancellationToken);
    }

    private async Task<Item?> PlaceCopyAsync(
        ItemPath source,
        Item destination,
        ConflictPolicy conflict,
        string createdBy,
        CancellationToken cancellationToken)
    {
        string? name = await _placement.ResolveNameAsync(destination, source.Item.Name, conflict, source, cancellationToken);
        return name is null ? null : await CopyTreeAsync(source.Item, destination, name, createdBy, cancellationToken);
    }

    private async Task<Item> CopyTreeAsync(
        Item source,
        Item destination,
        string name,
        string createdBy,
        CancellationToken cancellationToken)
    {
        List<string> written = [];
        CopyContext context = new(createdBy, _time.GetUtcNow(), written);
        List<Item> copies = [];
        Dictionary<Guid, Item> copiesBySource = [];
        try
        {
            foreach (Item item in await _store.ActiveSubtreeAsync(source.Id, cancellationToken))
            {
                Item copy = item.Id == source.Id || item.ParentId is not { } parentId
                    ? await CopyItemAsync(item, destination, name, context, cancellationToken)
                    : await CopyItemAsync(item, copiesBySource[parentId], item.Name, context, cancellationToken);
                copiesBySource[item.Id] = copy;
                copies.Add(copy);
            }

            await _store.SaveAsync(cancellationToken);
        }
        catch
        {
            written.ForEach(_blobs.Delete);
            throw;
        }

        await _previews.EnqueueAsync(copies, TaskPriority.Normal, cancellationToken);
        return copies[0];
    }

    private async Task<Item> CopyItemAsync(
        Item source,
        Item parent,
        string name,
        CopyContext context,
        CancellationToken cancellationToken)
    {
        Item copy = source.StorageKey is { } sourceKey
            ? Item.CreateFile(parent, await CopyBlobAsync(source, sourceKey, name, context.Written, cancellationToken), context.CreatedBy, context.Now)
            : Item.CreateFolder(parent, name, context.CreatedBy, context.Now);
        _store.Add(copy);
        return copy;
    }

    private async Task<StoredFile> CopyBlobAsync(
        Item source,
        string sourceKey,
        string name,
        List<string> written,
        CancellationToken cancellationToken)
    {
        Guid id = Guid.CreateVersion7();
        string key = StorageKey.For(source.OwnerId, id, name, source.Modified);
        await _blobs.CopyAsync(sourceKey, key, cancellationToken);
        written.Add(key);
        return new StoredFile(id, name, source.Size, key, source.Modified);
    }

    private sealed record CopyContext(string CreatedBy, DateTimeOffset Now, List<string> Written);
}
