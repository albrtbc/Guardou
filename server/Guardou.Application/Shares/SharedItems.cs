using Guardou.Application.Audit;
using Guardou.Application.Files;
using Guardou.Application.Previews;
using Guardou.Application.Users;
using Guardou.Domain.Files;
using Guardou.Domain.Previews;
using Guardou.Domain.Shares;

namespace Guardou.Application.Shares;

public sealed class SharedItems
{
    private readonly IShareStore _shares;
    private readonly IItemStore _store;
    private readonly OwnedItems _items;
    private readonly ItemEntryFactory _entries;
    private readonly ItemBrowser _browser;
    private readonly ItemDownloads _downloads;
    private readonly ItemPreviews _previews;
    private readonly IProfilePictureStore _pictures;
    private readonly AuditLog _audit;

    internal SharedItems(
        IShareStore shares,
        IItemStore store,
        OwnedItems items,
        ItemEntryFactory entries,
        ItemBrowser browser,
        ItemDownloads downloads,
        ItemPreviews previews,
        IProfilePictureStore pictures,
        AuditLog audit)
    {
        _shares = shares;
        _store = store;
        _items = items;
        _entries = entries;
        _browser = browser;
        _downloads = downloads;
        _previews = previews;
        _pictures = pictures;
        _audit = audit;
    }

    public async Task<ItemEntry> GetAsync(string token, CancellationToken cancellationToken)
    {
        (Share share, Item root) = await FindShareAsync(token, cancellationToken);
        _audit.SharedItemAccessed(share.Id, root.Id, ShareAccessKind.Open);
        return await _entries.CreateSharedAsync(root, root.Id, cancellationToken);
    }

    public async Task<ItemDetails> GetDetailsAsync(string token, string id, CancellationToken cancellationToken)
    {
        SharedAccess shared = await FindAsync(token, id, cancellationToken);
        _audit.SharedItemAccessed(shared.ShareId, shared.Access.Item.Id, ShareAccessKind.Details);
        return await _browser.DetailsAsync(shared.Access, content: false, cancellationToken);
    }

    public async Task<FileDownload?> TryOpenFileAsync(string token, string id, CancellationToken cancellationToken)
    {
        SharedAccess shared = await FindAsync(token, id, cancellationToken);
        FileDownload? file = await _downloads.TryOpenFileAsync(shared.Access.Item.OwnerId, id, cancellationToken);
        if (file is not null)
        {
            _audit.SharedItemAccessed(shared.ShareId, shared.Access.Item.Id, ShareAccessKind.Download);
        }

        return file;
    }

    public async Task<FileDownload> OpenFileAsync(string token, string id, CancellationToken cancellationToken)
    {
        SharedAccess shared = await FindAsync(token, id, cancellationToken);
        FileDownload file = await _downloads.OpenFileAsync(shared.Access.Item.OwnerId, id, cancellationToken);
        _audit.SharedItemAccessed(shared.ShareId, shared.Access.Item.Id, ShareAccessKind.View);
        return file;
    }

    public async Task<ItemArchive> PrepareArchiveAsync(string token, string id, CancellationToken cancellationToken)
    {
        SharedAccess shared = await FindAsync(token, id, cancellationToken);
        ItemArchive archive = await _downloads.PrepareArchiveAsync(shared.Access.Item.OwnerId, [id], cancellationToken);
        _audit.SharedItemAccessed(shared.ShareId, shared.Access.Item.Id, ShareAccessKind.Archive);
        return archive;
    }

    public async Task<Stream?> OpenPreviewAsync(string token, string id, PreviewSize size, CancellationToken cancellationToken)
    {
        SharedAccess shared = await FindAsync(token, id, cancellationToken);
        _audit.SharedItemAccessed(shared.ShareId, shared.Access.Item.Id, ShareAccessKind.Preview);
        return await _previews.OpenAsync(shared.Access.Item.OwnerId, id, size, cancellationToken);
    }

    public async Task<ProfilePicture?> OpenCreatorPictureAsync(string token, string id, CancellationToken cancellationToken)
    {
        SharedAccess shared = await FindAsync(token, id, cancellationToken);
        _audit.SharedItemAccessed(shared.ShareId, shared.Access.Item.Id, ShareAccessKind.CreatorPicture);
        return _pictures.TryOpen(shared.Access.Item.CreatedBy);
    }

    private static ShareNotFoundException NotFound()
    {
        return new ShareNotFoundException("The share does not exist.");
    }

    private async Task<SharedAccess> FindAsync(string token, string id, CancellationToken cancellationToken)
    {
        (Share share, Item root) = await FindShareAsync(token, cancellationToken);
        ItemPath? path = ItemId.TryParse(id, out Guid itemId)
            ? await _items.TryFindActiveAsync(root.OwnerId, itemId, cancellationToken)
            : null;
        return path is not null && path.Contains(root.Id)
            ? new SharedAccess(share.Id, new ItemAccess(path, root.Id, ViewerId: null))
            : throw NotFound();
    }

    internal async Task<Item> FindRootAsync(string token, CancellationToken cancellationToken)
    {
        return (await FindShareAsync(token, cancellationToken)).Root;
    }

    private async Task<(Share Share, Item Root)> FindShareAsync(string token, CancellationToken cancellationToken)
    {
        Share share = (ShareToken.IsValid(token) ? await _shares.FindByTokenAsync(token, cancellationToken) : null) ?? throw NotFound();
        Item item = await _store.FindAnyAsync(share.ItemId, cancellationToken) ?? throw NotFound();
        ItemPath? path = await _items.TryFindActiveAsync(item.OwnerId, item.Id, cancellationToken);
        return (share, path?.Item ?? throw NotFound());
    }

    private sealed record SharedAccess(Guid ShareId, ItemAccess Access);
}
