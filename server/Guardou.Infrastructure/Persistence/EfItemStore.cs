using Guardou.Application.Files;
using Guardou.Domain.Files;
using Guardou.Infrastructure.Indexing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Guardou.Infrastructure.Persistence;

internal sealed class EfItemStore(AppDbContext context, ItemIndexWriter index, StorageUsageCounter usage) : IItemStore
{
    private DbSet<Item> Items => context.Set<Item>();

    public Task<Item?> FindAsync(string ownerId, Guid id, CancellationToken cancellationToken)
    {
        return Items.SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken);
    }

    public Task<Item?> FindAnyAsync(Guid id, CancellationToken cancellationToken)
    {
        return Items.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public Task<Item?> FindRootAsync(string ownerId, CancellationToken cancellationToken)
    {
        return Items.SingleOrDefaultAsync(item => item.OwnerId == ownerId && item.ParentId == null, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> RootIdsAsync(CancellationToken cancellationToken)
    {
        return await Items.Where(item => item.ParentId == null).Select(item => item.Id).ToListAsync(cancellationToken);
    }

    public Task<Item?> FindChildAsync(Guid parentId, string name, CancellationToken cancellationToken)
    {
        return Items.SingleOrDefaultAsync(
            item => item.ParentId == parentId && item.Name == name && item.Trashed == null,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Item>> ChildrenAsync(Guid parentId, CancellationToken cancellationToken)
    {
        return await Items.Where(item => item.ParentId == parentId && item.Trashed == null).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Item>> ChildrenWithTrashAsync(Guid parentId, CancellationToken cancellationToken)
    {
        return await Items.Where(item => item.ParentId == parentId).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ChildFolderIdsAsync(IReadOnlyCollection<Guid> parentIds, CancellationToken cancellationToken)
    {
        List<Guid?> parents = parentIds.Select(id => (Guid?)id).ToList();
        return await Items
            .AsNoTracking()
            .Where(item => item.Kind == ItemKind.Folder && parents.Contains(item.ParentId))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Item>> StarredAsync(string ownerId, CancellationToken cancellationToken)
    {
        return await Items.Where(item => item.OwnerId == ownerId && item.Starred && item.Trashed == null).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Item>> TrashedAsync(string ownerId, CancellationToken cancellationToken)
    {
        return await Items.Where(item => item.OwnerId == ownerId && item.Trashed != null).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Item>> TrashedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        return await Items.Where(item => item.Trashed != null && item.Trashed < cutoff).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Item>> ActiveFilesAsync(Guid? after, int limit, CancellationToken cancellationToken)
    {
        return await Items
            .AsNoTracking()
            .Where(item => item.Kind == ItemKind.File && item.Trashed == null && (after == null || item.Id.CompareTo(after.Value) > 0))
            .OrderBy(item => item.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Item>> RecentAsync(
        string userId,
        ItemKind kind,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        IQueryable<ItemOpen> opens = context.Set<ItemOpen>().Where(open => open.UserId == userId);
        return await Items
            .AsNoTracking()
            .Where(item => item.Kind == kind && item.ParentId != null && item.Trashed == null)
            .Select(item => new
            {
                Item = item,
                Opened = opens.Where(open => open.ItemId == item.Id).Select(open => (DateTimeOffset?)open.Opened).FirstOrDefault(),
            })
            .Where(candidate => candidate.Item.CreatedBy == userId || candidate.Opened != null)
            .OrderByDescending(candidate => candidate.Opened > candidate.Item.Created ? candidate.Opened : candidate.Item.Created)
            .ThenByDescending(candidate => candidate.Item.Id)
            .Skip(skip)
            .Take(take)
            .Select(candidate => candidate.Item)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> StorageKeysAsync(string ownerId, CancellationToken cancellationToken)
    {
        List<string?> keys = await Items.Where(item => item.OwnerId == ownerId).Select(item => item.StorageKey).ToListAsync(cancellationToken);
        return keys.OfType<string>().ToList();
    }

    public void Add(Item item)
    {
        Items.Add(item);
    }

    public void Remove(Item item)
    {
        Items.Remove(item);
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, long> changes = usage.PendingChanges();
        if (changes.Count == 0)
        {
            await index.SaveAsync(cancellationToken);
            return;
        }

        await using IDbContextTransaction? transaction = context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await usage.ApplyAsync(changes, cancellationToken);
        await index.SaveAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }
}
