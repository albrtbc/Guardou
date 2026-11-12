using System.IO.Compression;
using Guardou.Domain.Files;

namespace Guardou.Application.Files;

public sealed class ItemDownloads
{
    private const string ArchiveExtension = ".zip";
    private const string DefaultArchiveName = "download";

    private readonly IBlobStore _blobs;
    private readonly IMimeTypeMap _mimeTypes;
    private readonly AccessibleItems _items;
    private readonly ItemTree _tree;
    private readonly ArchiveLimits _limits;
    private readonly ArchiveSlots _slots;

    internal ItemDownloads(
        IBlobStore blobs,
        IMimeTypeMap mimeTypes,
        AccessibleItems items,
        ItemTree tree,
        ArchiveLimits limits,
        ArchiveSlots slots)
    {
        _blobs = blobs;
        _mimeTypes = mimeTypes;
        _items = items;
        _tree = tree;
        _limits = limits;
        _slots = slots;
    }

    public async Task<FileDownload> OpenFileAsync(string userId, string id, CancellationToken cancellationToken)
    {
        return await TryOpenFileAsync(userId, id, cancellationToken)
            ?? throw new InvalidItemRequestException("A folder has no content.");
    }

    public async Task<FileDownload?> TryOpenFileAsync(string userId, string id, CancellationToken cancellationToken)
    {
        Item item = (await _items.FindActiveAsync(userId, id, cancellationToken)).Item;
        return item.StorageKey is { } key
            ? new FileDownload(item.Name, _mimeTypes.MimeTypeOf(item.Name), item.Modified, _blobs.OpenRead(key))
            : null;
    }

    public async Task<ItemArchive> PrepareArchiveAsync(string userId, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            throw new InvalidItemRequestException("Select at least one item.");
        }

        List<Item> roots = await SelectedRootsAsync(userId, ids, cancellationToken);
        List<TreeNode> nodes = [];
        CaseInsensitiveNames rootNames = new();
        long bytes = 0;
        foreach (Item item in roots)
        {
            string name = rootNames.Claim(item.IsRoot ? DefaultArchiveName : item.Name);
            IReadOnlyList<TreeNode> subtree = await _tree.ActiveSubtreeAsync(item, name, _limits.MaximumEntries - nodes.Count, cancellationToken);
            nodes.AddRange(subtree);
            bytes += subtree.Sum(node => node.Item.Size);
            if (nodes.Count > _limits.MaximumEntries || bytes > _limits.MaximumBytes)
            {
                throw new ArchiveTooLargeException(_limits);
            }
        }

        return new ItemArchive(ArchiveName(roots), nodes);
    }

    public IDisposable AcquireArchiveSlot(ArchiveAudience audience)
    {
        return _slots.Acquire(audience);
    }

    public async Task WriteArchiveAsync(ItemArchive archive, Stream output, CancellationToken cancellationToken)
    {
        await using ZipArchive zip = await ZipArchive.CreateAsync(output, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: null, cancellationToken);
        foreach (TreeNode node in archive.Nodes)
        {
            await WriteEntryAsync(zip, node, cancellationToken);
        }
    }

    private async Task<List<Item>> SelectedRootsAsync(string userId, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        List<ItemAccess> selected = [];
        HashSet<Guid> selectedIds = [];
        foreach (string id in ids)
        {
            ItemAccess access = await _items.FindActiveAsync(userId, id, cancellationToken);
            if (selectedIds.Add(access.Item.Id))
            {
                selected.Add(access);
            }
        }

        return selected
            .Where(access => !access.Path.Ancestors.Any(ancestor => selectedIds.Contains(ancestor.Id)))
            .Select(access => access.Item)
            .ToList();
    }

    private static string ArchiveName(List<Item> roots)
    {
        string name = roots is [{ IsFolder: true, IsRoot: false } folder] ? folder.Name : DefaultArchiveName;
        return name + ArchiveExtension;
    }

    private async Task WriteEntryAsync(ZipArchive zip, TreeNode node, CancellationToken cancellationToken)
    {
        string? key = node.Item.StorageKey;
        ZipArchiveEntry entry = key is null
            ? zip.CreateEntry(node.Path + "/")
            : zip.CreateEntry(node.Path, CompressionLevel.NoCompression);
        entry.LastWriteTime = node.Item.Modified;
        await using Stream target = await entry.OpenAsync(cancellationToken);
        if (key is not null)
        {
            await using Stream source = _blobs.OpenRead(key);
            await source.CopyToAsync(target, cancellationToken);
        }
    }
}
