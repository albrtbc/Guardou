using Guardou.Domain.Files;

namespace Guardou.Application.Files;

public interface IItemStore
{
    Task<Item?> FindAsync(string ownerId, Guid id, CancellationToken cancellationToken);

    Task<Item?> FindAnyAsync(Guid id, CancellationToken cancellationToken);

    Task<Item?> FindRootAsync(string ownerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> RootIdsAsync(CancellationToken cancellationToken);

    Task<Item?> FindChildAsync(Guid parentId, string name, CancellationToken cancellationToken);

    Task<IReadOnlyList<Item>> ChildrenAsync(Guid parentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Item>> AncestryAsync(string ownerId, Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Item>> SubtreeAsync(Guid rootId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Item>> ActiveSubtreeAsync(Guid rootId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Item>> ActiveSubtreeAsync(Guid rootId, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> ChildFolderIdsAsync(IReadOnlyCollection<Guid> parentIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<Item>> StarredAsync(string ownerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Item>> TrashedAsync(string ownerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Item>> TrashedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

    Task<IReadOnlyList<Item>> ActiveFilesAsync(Guid? after, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<Item>> RecentAsync(string userId, ItemKind kind, int skip, int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> StorageKeysAsync(string ownerId, CancellationToken cancellationToken);

    void Add(Item item);

    void Remove(Item item);

    Task SaveAsync(CancellationToken cancellationToken);
}
