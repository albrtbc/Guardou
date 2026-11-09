using Guardou.Application.Previews;
using Guardou.Application.Search;
using Guardou.Application.Shares;
using Guardou.Application.Users;
using Guardou.Domain.Files;

namespace Guardou.Application.Files;

internal sealed class ItemEntryFactory(
    IMimeTypeMap mimeTypes,
    IPreviewRenderer previews,
    IUserNames users,
    IProfilePictureStore pictures,
    IItemOpenStore opens,
    IItemIndex index,
    IShareLookup shares)
{
    private static readonly ItemPermissions ReadOnly = new(CanModify: false, CanAddChildren: false);

    private readonly Dictionary<string, ItemCreator> _creators = [];

    public async Task<ItemEntry> CreateAsync(Item item, string viewerId, CancellationToken cancellationToken)
    {
        return (await CreateAsync([item], viewerId, cancellationToken)).Single();
    }

    public async Task<ItemEntry> CreateSharedAsync(Item item, Guid shareRootId, CancellationToken cancellationToken)
    {
        return (await CreateSharedAsync([item], shareRootId, viewerId: null, cancellationToken)).Single();
    }

    public async Task<IReadOnlyList<ItemEntry>> CreateSharedRootsAsync(
        IEnumerable<Item> items,
        string viewerId,
        CancellationToken cancellationToken)
    {
        return ReadOnlyEntries(await CreateAsync(items, viewerId, cancellationToken), _ => true);
    }

    public Task<IReadOnlyList<ItemEntry>> CreateAsync(IEnumerable<Item> items, ItemAccess access, CancellationToken cancellationToken)
    {
        return access.ShareRootId is { } shareRootId
            ? CreateSharedAsync(items, shareRootId, access.ViewerId, cancellationToken)
            : CreateAsync(items, access.ViewerId, cancellationToken);
    }

    public static Breadcrumb Breadcrumb(Item item)
    {
        return new Breadcrumb(ItemId.Format(item.Id), item.Name);
    }

    public async Task<IReadOnlyList<ItemEntry>> CreateAsync(
        IEnumerable<Item> items,
        string? viewerId,
        CancellationToken cancellationToken)
    {
        List<Item> ordered = Ordered(items).ToList();
        await LoadCreatorsAsync(ordered, cancellationToken);
        IReadOnlyDictionary<Guid, DateTimeOffset> opened = await OpenedAsync(ordered, viewerId, cancellationToken);
        IReadOnlyDictionary<Guid, long> sizes = await FolderSizesAsync(ordered, cancellationToken);
        IReadOnlySet<Guid> shared = await SharedByViewerAsync(ordered, viewerId, cancellationToken);
        return ordered
            .Select(item => Create(
                item,
                sizes.GetValueOrDefault(item.Id, item.Size),
                opened.TryGetValue(item.Id, out DateTimeOffset time) ? time : null,
                shared.Contains(item.Id)))
            .ToList();
    }

    private async Task<IReadOnlySet<Guid>> SharedByViewerAsync(
        List<Item> items,
        string? viewerId,
        CancellationToken cancellationToken)
    {
        List<Guid> owned = items.Where(item => item.OwnerId == viewerId).Select(item => item.Id).ToList();
        return owned.Count == 0 ? new HashSet<Guid>() : await shares.SharedAsync(owned, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, long>> FolderSizesAsync(List<Item> items, CancellationToken cancellationToken)
    {
        List<Guid> folders = items.Where(item => item.IsFolder).Select(item => item.Id).ToList();
        return folders.Count == 0 ? new Dictionary<Guid, long>() : await index.FolderSizesAsync(folders, cancellationToken);
    }

    private async Task<IReadOnlyList<ItemEntry>> CreateSharedAsync(
        IEnumerable<Item> items,
        Guid shareRootId,
        string? viewerId,
        CancellationToken cancellationToken)
    {
        string rootId = ItemId.Format(shareRootId);
        IReadOnlyList<ItemEntry> entries = await CreateAsync(items, viewerId, cancellationToken);
        return ReadOnlyEntries(viewerId is null ? entries.Select(Anonymous) : entries, entry => entry.Id == rootId);
    }

    private static ItemEntry Anonymous(ItemEntry entry)
    {
        return entry with { Creator = entry.Creator with { Id = null } };
    }

    private static List<ItemEntry> ReadOnlyEntries(IEnumerable<ItemEntry> entries, Func<ItemEntry, bool> isShareRoot)
    {
        return entries
            .Select(entry => entry with
            {
                ParentId = isShareRoot(entry) ? null : entry.ParentId,
                Starred = false,
                Permissions = ReadOnly,
            })
            .ToList();
    }

    private static IEnumerable<Item> Ordered(IEnumerable<Item> items)
    {
        return items
            .OrderBy(item => item.Kind == ItemKind.File)
            .ThenBy(item => item.Name, StringComparer.Ordinal);
    }

    private async Task LoadCreatorsAsync(IEnumerable<Item> items, CancellationToken cancellationToken)
    {
        List<string> missing = items.Select(item => item.CreatedBy).Distinct().Where(id => !_creators.ContainsKey(id)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        foreach ((string id, string displayName) in await users.DisplayNamesAsync(missing, cancellationToken))
        {
            _creators[id] = new ItemCreator(id, displayName, pictures.Contains(id));
        }
    }

    private async Task<IReadOnlyDictionary<Guid, DateTimeOffset>> OpenedAsync(
        List<Item> items,
        string? viewerId,
        CancellationToken cancellationToken)
    {
        return viewerId is null || items.Count == 0
            ? new Dictionary<Guid, DateTimeOffset>()
            : await opens.OpenedAsync(viewerId, items.Select(item => item.Id).ToList(), cancellationToken);
    }

    private ItemEntry Create(Item item, long size, DateTimeOffset? opened, bool sharedByMe)
    {
        return new ItemEntry(
            ItemId.Format(item.Id),
            item.ParentId is { } parentId ? ItemId.Format(parentId) : null,
            item.Name,
            item.Kind,
            ItemTypes.Of(item, mimeTypes),
            size,
            item.IsFolder ? null : mimeTypes.MimeTypeOf(item.Name),
            item.Modified,
            item.Created,
            opened,
            item.Starred,
            sharedByMe,
            _creators[item.CreatedBy],
            HasPreview(item),
            new ItemPermissions(CanModify: !item.IsRoot, CanAddChildren: item.IsFolder));
    }

    private bool HasPreview(Item item)
    {
        return !item.IsFolder && previews.Supports(item.Name);
    }
}
