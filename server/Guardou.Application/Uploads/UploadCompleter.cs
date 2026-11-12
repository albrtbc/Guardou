using Guardou.Application.Files;
using Guardou.Application.Previews;
using Guardou.Domain.Files;
using Guardou.Domain.Tasks;

namespace Guardou.Application.Uploads;

public sealed class UploadCompleter
{
    private readonly IUploadStore _uploads;
    private readonly IUploadContent _content;
    private readonly OwnedUploads _owned;
    private readonly IItemStore _store;
    private readonly IBlobStore _blobs;
    private readonly OwnedItems _items;
    private readonly ItemPlacement _placement;
    private readonly ItemMoveLocks _moveLocks;
    private readonly ItemEntryFactory _entries;
    private readonly PreviewTasks _previews;
    private readonly TimeProvider _time;

    internal UploadCompleter(
        IUploadStore uploads,
        IUploadContent content,
        OwnedUploads owned,
        IItemStore store,
        IBlobStore blobs,
        OwnedItems items,
        ItemPlacement placement,
        ItemMoveLocks moveLocks,
        ItemEntryFactory entries,
        PreviewTasks previews,
        TimeProvider time)
    {
        _uploads = uploads;
        _content = content;
        _owned = owned;
        _store = store;
        _blobs = blobs;
        _items = items;
        _placement = placement;
        _moveLocks = moveLocks;
        _entries = entries;
        _previews = previews;
        _time = time;
    }

    public async Task<ItemEntry?> CompleteAsync(string ownerId, string id, ConflictPolicy conflict, CancellationToken cancellationToken)
    {
        Upload upload = await _owned.FindAsync(ownerId, id, cancellationToken);
        if (!await _content.IsCompleteAsync(upload.Id, cancellationToken))
        {
            throw new InvalidItemRequestException("The upload is not complete.");
        }

        Item folder = await EnsureFoldersWithinDepthAsync(ownerId, upload, cancellationToken);
        string? placedName = await _placement.ResolveNameAsync(folder, upload.Name, conflict, null, cancellationToken);
        Item? item = placedName is null ? null : await StoreAsync(folder, upload, placedName, cancellationToken);
        await ForgetAsync(upload, cancellationToken);
        if (item is null)
        {
            return null;
        }

        await _previews.EnqueueAsync([item], TaskPriority.Normal, cancellationToken);
        return await _entries.CreateAsync(item, ownerId, cancellationToken);
    }

    private async Task<Item> EnsureFoldersWithinDepthAsync(string ownerId, Upload upload, CancellationToken cancellationToken)
    {
        SemaphoreSlim ownerLock = _moveLocks.For(ownerId);
        await ownerLock.WaitAsync(cancellationToken);
        try
        {
            ItemPath target = await _items.TryFindActiveAsync(ownerId, upload.FolderId, cancellationToken)
                ?? throw OwnedItems.NotFound(ItemId.Format(upload.FolderId));
            FolderDepth.EnsureWithin(target.Ancestors.Count + upload.Destination.FolderNames.Count);
            return await EnsureFoldersAsync(target.Item, upload, cancellationToken);
        }
        finally
        {
            ownerLock.Release();
        }
    }

    private async Task<Item> StoreAsync(Item folder, Upload upload, string name, CancellationToken cancellationToken)
    {
        DateTimeOffset now = _time.GetUtcNow();
        DateTimeOffset modified = upload.Modified ?? now;
        Guid fileId = Guid.CreateVersion7();
        string key = StorageKey.For(folder.OwnerId, fileId, name, modified);
        await _content.MoveToBlobAsync(upload.Id, key, modified, cancellationToken);
        Item item = Item.CreateFile(folder, new StoredFile(fileId, name, upload.Size, key, modified), upload.OwnerId, now);
        _store.Add(item);
        try
        {
            await _store.SaveAsync(cancellationToken);
        }
        catch
        {
            _blobs.Delete(key);
            throw;
        }

        return item;
    }

    private async Task ForgetAsync(Upload upload, CancellationToken cancellationToken)
    {
        await _content.DeleteAsync(upload.Id, cancellationToken);
        _uploads.Remove(upload);
        await _uploads.SaveAsync(cancellationToken);
    }

    private async Task<Item> EnsureFoldersAsync(Item folder, Upload upload, CancellationToken cancellationToken)
    {
        foreach (string name in upload.Destination.FolderNames)
        {
            Item? existing = await _store.FindChildAsync(folder.Id, name, cancellationToken);
            if (existing is { IsFolder: false })
            {
                throw ItemConflictException.ForName(name);
            }

            if (existing is null)
            {
                existing = Item.CreateFolder(folder, name, upload.OwnerId, _time.GetUtcNow());
                _store.Add(existing);
                await _store.SaveAsync(cancellationToken);
            }

            folder = existing;
        }

        return folder;
    }
}
