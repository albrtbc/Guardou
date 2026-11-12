using Guardou.Application.Files;
using Guardou.Application.Quotas;
using Guardou.Application.Settings;
using Guardou.Domain.Files;
using Guardou.Domain.Settings;

namespace Guardou.Application.Uploads;

public sealed class UploadSessions
{
    private readonly IUploadStore _uploads;
    private readonly IUploadContent _content;
    private readonly OwnedUploads _owned;
    private readonly OwnedItems _items;
    private readonly IItemStore _store;
    private readonly StorageReservations _reservations;
    private readonly EffectiveSettings<ServerSettings> _settings;
    private readonly TimeProvider _time;

    internal UploadSessions(
        IUploadStore uploads,
        IUploadContent content,
        OwnedUploads owned,
        OwnedItems items,
        IItemStore store,
        StorageReservations reservations,
        EffectiveSettings<ServerSettings> settings,
        TimeProvider time)
    {
        _uploads = uploads;
        _content = content;
        _owned = owned;
        _items = items;
        _store = store;
        _reservations = reservations;
        _settings = settings;
        _time = time;
    }

    public async Task<UploadSession?> StartAsync(string ownerId, UploadRequest request, CancellationToken cancellationToken)
    {
        ItemPath target = await _items.FindActiveFolderAsync(ownerId, request.FolderId, cancellationToken);
        UploadDestination destination = UploadDestination.Of(
            target.Item.Id,
            UploadPath.FolderNames(request.RelativePath, target.Ancestors.Count),
            ItemPlacement.ValidName(request.FileName));
        await EnsureNoActiveUploadAsync(destination, cancellationToken);
        if (await IsTakenAsync(target.Item, destination, cancellationToken))
        {
            switch (request.Conflict)
            {
                case ConflictPolicy.Skip:
                    return null;
                case ConflictPolicy.Fail:
                    throw ItemConflictException.ForName(destination.Name);
            }
        }

        await EnsureNotTooLargeAsync(request.Size, cancellationToken);
        return await _reservations.ReserveAsync(
            ownerId,
            request.Size,
            () => CreateAsync(ownerId, destination, request, cancellationToken),
            cancellationToken);
    }

    public async Task CancelAsync(string ownerId, string id, CancellationToken cancellationToken)
    {
        Upload upload = await _owned.FindAsync(ownerId, id, cancellationToken);
        await _content.DeleteAsync(upload.Id, cancellationToken);
        _uploads.Remove(upload);
        await _uploads.SaveAsync(cancellationToken);
    }

    public async Task<bool> OwnsAsync(string ownerId, string id, CancellationToken cancellationToken)
    {
        Upload? upload = await _uploads.FindAsync(id, cancellationToken);
        return upload?.OwnerId == ownerId;
    }

    public Task<bool> HasRoomToWriteAsync(long bytes, CancellationToken cancellationToken)
    {
        return _reservations.HasRoomToWriteAsync(bytes, cancellationToken);
    }

    public async Task RecordWriteAsync(string id, CancellationToken cancellationToken)
    {
        Upload? upload = await _uploads.FindAsync(id, cancellationToken);
        upload?.RecordWrite(_time.GetUtcNow());
        await _uploads.SaveAsync(cancellationToken);
    }

    private async Task<UploadSession?> CreateAsync(
        string ownerId,
        UploadDestination destination,
        UploadRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureBelowTheUploadCapAsync(ownerId, cancellationToken);
        string id = await _content.CreateAsync(request.Size, cancellationToken);
        _uploads.Add(Upload.Create(id, ownerId, destination, request.Size, request.Modified, _time.GetUtcNow()));
        await _uploads.SaveAsync(cancellationToken);
        return new UploadSession(id);
    }

    private async Task EnsureNotTooLargeAsync(long size, CancellationToken cancellationToken)
    {
        if ((await _settings.GetAsync(cancellationToken)).MaxFileSizeMb is { } maxFileSizeMb && size > StoragePolicy.BytesOf(maxFileSizeMb))
        {
            throw new FileTooLargeException(maxFileSizeMb);
        }
    }

    private async Task EnsureBelowTheUploadCapAsync(string ownerId, CancellationToken cancellationToken)
    {
        DateTimeOffset since = Upload.ActiveSince(_time.GetUtcNow());
        if (await _uploads.CountActiveOfOwnerAsync(ownerId, since, cancellationToken) >= StoragePolicy.MaximumUploadsInProgress)
        {
            throw new TooManyUploadsException();
        }
    }

    private async Task EnsureNoActiveUploadAsync(UploadDestination destination, CancellationToken cancellationToken)
    {
        DateTimeOffset now = _time.GetUtcNow();
        IReadOnlyList<Upload> uploads = await _uploads.ToDestinationAsync(destination, cancellationToken);
        if (uploads.Any(upload => upload.IsActive(now)))
        {
            throw new UploadInProgressException(destination.Name);
        }
    }

    private async Task<bool> IsTakenAsync(Item folder, UploadDestination destination, CancellationToken cancellationToken)
    {
        Item? current = folder;
        foreach (string name in destination.FolderNames)
        {
            current = await _store.FindChildAsync(current.Id, name, cancellationToken);
            if (current is null)
            {
                return false;
            }

            if (!current.IsFolder)
            {
                throw ItemConflictException.ForName(name);
            }
        }

        return await _store.FindChildAsync(current.Id, destination.Name, cancellationToken) is not null;
    }
}
