using Guardou.Application.Files;
using Guardou.Application.Search;
using Guardou.Domain.Files;
using Guardou.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Guardou.Infrastructure.Indexing;

internal sealed partial class ItemIndexWriter(
    AppDbContext app,
    IndexDbContext index,
    IMimeTypeMap mimeTypes,
    ILogger<ItemIndexWriter> logger) : IItemIndexRebuilder
{
    private DbSet<Item> Items => app.Set<Item>();

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        List<Guid> changed = app.ChangeTracker.Entries<Item>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => entry.Entity.Id)
            .Distinct()
            .ToList();
        await app.SaveChangesAsync(cancellationToken);
        if (changed.Count > 0)
        {
            await UpdateAsync(changed, cancellationToken);
        }
    }

    public async Task RemoveOwnerAsync(string ownerId, CancellationToken cancellationToken)
    {
        await index.Entries.Where(entry => entry.OwnerId == ownerId).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task RebuildIfEmptyAsync(CancellationToken cancellationToken)
    {
        if (await index.Entries.AnyAsync(cancellationToken))
        {
            return;
        }

        await InTransactionAsync(
            async () =>
            {
                foreach (Item root in await Items.AsNoTracking().Where(item => item.ParentId == null).ToListAsync(cancellationToken))
                {
                    await InsertSubtreeAsync(root, parentPath: null, cancellationToken);
                }
            },
            cancellationToken);
    }

    public Task RebuildAsync(Guid rootId, CancellationToken cancellationToken)
    {
        return InTransactionAsync(
            async () =>
            {
                if (await Items.AsNoTracking().SingleOrDefaultAsync(item => item.Id == rootId && item.ParentId == null, cancellationToken) is { } root)
                {
                    await RemoveOwnerAsync(root.OwnerId, cancellationToken);
                    await InsertSubtreeAsync(root, parentPath: null, cancellationToken);
                }
            },
            cancellationToken);
    }

    private async Task InTransactionAsync(Func<Task> work, CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = await index.Database.BeginTransactionAsync(cancellationToken);
        await work();
        await transaction.CommitAsync(cancellationToken);
        index.ChangeTracker.Clear();
    }

    private Task UpdateAsync(List<Guid> changed, CancellationToken cancellationToken)
    {
        return InTransactionAsync(() => ApplyChangesAsync(changed, cancellationToken), cancellationToken);
    }

    private async Task ApplyChangesAsync(List<Guid> changed, CancellationToken cancellationToken)
    {
        List<IndexEntry> previous = await index.Entries.Where(entry => changed.Contains(entry.ItemId)).ToListAsync(cancellationToken);
        Dictionary<Guid, Item> current = await Items.AsNoTracking()
            .Where(item => changed.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        List<IndexEntry> relocated = UpdateInPlace(previous, current);
        await index.SaveChangesAsync(cancellationToken);
        foreach (IndexEntry entry in Outermost(relocated))
        {
            await RemoveSubtreeAsync(entry, cancellationToken);
        }

        HashSet<Guid> unchangedPlacement = previous.Except(relocated).Select(entry => entry.ItemId).ToHashSet();
        List<Item> placed = current.Values
            .Where(item => item.Trashed is null && !unchangedPlacement.Contains(item.Id))
            .ToList();
        HashSet<Guid> placedIds = placed.Select(item => item.Id).ToHashSet();
        foreach (Item item in placed.Where(item => item.ParentId is not { } parentId || !placedIds.Contains(parentId)))
        {
            await PlaceAsync(item, cancellationToken);
        }
    }

    private List<IndexEntry> UpdateInPlace(List<IndexEntry> previous, Dictionary<Guid, Item> current)
    {
        List<IndexEntry> relocated = [];
        foreach (IndexEntry entry in previous)
        {
            if (current.GetValueOrDefault(entry.ItemId) is { Trashed: null } item && item.ParentId == entry.ParentId)
            {
                entry.Name = item.Name;
                entry.Type = ItemTypes.Of(item, mimeTypes);
                entry.Modified = item.Modified;
                entry.Starred = item.Starred;
            }
            else
            {
                relocated.Add(entry);
            }
        }

        return relocated;
    }

    private static IEnumerable<IndexEntry> Outermost(List<IndexEntry> entries)
    {
        return entries.Where(entry => !entries.Any(other => other != entry && IndexPath.Contains(other.Path, entry.Path)));
    }

    private async Task RemoveSubtreeAsync(IndexEntry entry, CancellationToken cancellationToken)
    {
        await AddSizeAsync(IndexPath.AncestorIds(entry.Path), -entry.Size, cancellationToken);
        await index.Entries.Where(IndexPath.WithinAny([entry.Path])).ExecuteDeleteAsync(cancellationToken);
    }

    private async Task PlaceAsync(Item item, CancellationToken cancellationToken)
    {
        if (item.ParentId is not { } parentId)
        {
            await InsertSubtreeAsync(item, parentPath: null, cancellationToken);
            return;
        }

        string? parentPath = await index.Entries.AsNoTracking()
            .Where(entry => entry.ItemId == parentId)
            .Select(entry => entry.Path)
            .SingleOrDefaultAsync(cancellationToken);
        if (parentPath is not null)
        {
            long size = await InsertSubtreeAsync(item, parentPath, cancellationToken);
            await AddSizeAsync(IndexPath.Ids(parentPath), size, cancellationToken);
        }
    }

    private async Task<long> InsertSubtreeAsync(Item root, string? parentPath, CancellationToken cancellationToken)
    {
        List<IndexNode> nodes = await LoadSubtreeAsync(root, IndexPath.Of(parentPath, root.Id), cancellationToken);
        Dictionary<Guid, long> sizes = FolderSizes(nodes);
        List<Guid> ids = nodes.Select(node => node.Item.Id).ToList();
        await index.Entries.Where(entry => ids.Contains(entry.ItemId)).ExecuteDeleteAsync(cancellationToken);
        index.Entries.AddRange(nodes.Select(node => ToEntry(node, sizes.GetValueOrDefault(node.Item.Id, node.Item.Size))));
        await index.SaveChangesAsync(cancellationToken);
        return sizes.GetValueOrDefault(root.Id, root.Size);
    }

    private async Task<List<IndexNode>> LoadSubtreeAsync(Item root, string rootPath, CancellationToken cancellationToken)
    {
        List<IndexNode> nodes = [new IndexNode(root, rootPath)];
        HashSet<Guid> visited = [root.Id];
        List<IndexNode> level = nodes;
        bool broken = false;
        for (int depth = 1; level.Where(node => node.Item.IsFolder).ToDictionary(node => node.Item.Id, node => node.Path) is { Count: > 0 } folders; depth++)
        {
            if (depth > TreeLimits.MaximumWalkDepth)
            {
                broken = true;
                break;
            }

            List<Guid?> parentIds = folders.Keys.Select(id => (Guid?)id).ToList();
            List<Item> children = await Items.AsNoTracking()
                .Where(item => parentIds.Contains(item.ParentId) && item.Trashed == null)
                .ToListAsync(cancellationToken);
            List<Item> unvisited = children.Where(child => visited.Add(child.Id)).ToList();
            broken |= unvisited.Count < children.Count;
            level = unvisited
                .Select(child => new IndexNode(child, IndexPath.Of(folders.GetValueOrDefault(child.ParentId ?? Guid.Empty), child.Id)))
                .ToList();
            nodes.AddRange(level);
        }

        if (broken)
        {
            LogBrokenTree(root.Id);
        }

        return nodes;
    }

    private static Dictionary<Guid, long> FolderSizes(List<IndexNode> nodes)
    {
        Dictionary<Guid, long> sizes = nodes.Where(node => node.Item.IsFolder).ToDictionary(node => node.Item.Id, _ => 0L);
        for (int position = nodes.Count - 1; position > 0; position--)
        {
            Item item = nodes[position].Item;
            if (item.ParentId is { } parentId && sizes.ContainsKey(parentId))
            {
                sizes[parentId] += sizes.GetValueOrDefault(item.Id, item.Size);
            }
        }

        return sizes;
    }

    private IndexEntry ToEntry(IndexNode node, long size)
    {
        return new IndexEntry
        {
            ItemId = node.Item.Id,
            OwnerId = node.Item.OwnerId,
            ParentId = node.Item.ParentId,
            Path = node.Path,
            Name = node.Item.Name,
            Type = ItemTypes.Of(node.Item, mimeTypes),
            Size = size,
            Modified = node.Item.Modified,
            Starred = node.Item.Starred,
        };
    }

    private async Task AddSizeAsync(IReadOnlyList<Guid> itemIds, long delta, CancellationToken cancellationToken)
    {
        if (delta != 0 && itemIds.Count > 0)
        {
            await index.Entries
                .Where(entry => itemIds.Contains(entry.ItemId))
                .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.Size, entry => entry.Size + delta), cancellationToken);
        }
    }

    private sealed record IndexNode(Item Item, string Path);

    [LoggerMessage(Level = LogLevel.Error, Message = "The folder tree under item {ItemId} loops back on itself or is deeper than the limit, so the index skips the repeated items.")]
    private partial void LogBrokenTree(Guid itemId);
}
