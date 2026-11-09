using Guardou.Domain.Files;
using Microsoft.Extensions.Logging;

namespace Guardou.Application.Files;

internal sealed partial class OwnedItems(IItemStore store, ILogger<OwnedItems> logger)
{
    public const string RootAlias = "root";

    public async Task<Item> RootAsync(string ownerId, CancellationToken cancellationToken)
    {
        return await store.FindRootAsync(ownerId, cancellationToken)
            ?? throw new InvalidOperationException($"The user '{ownerId}' has no root folder.");
    }

    public async Task<ItemPath> FindActiveAsync(string ownerId, string reference, CancellationToken cancellationToken)
    {
        if (reference == RootAlias)
        {
            return new ItemPath(await RootAsync(ownerId, cancellationToken), []);
        }

        return await TryFindActiveAsync(ownerId, ParseId(reference), cancellationToken) ?? throw NotFound(reference);
    }

    public async Task<ItemPath?> TryFindActiveAsync(string ownerId, Guid id, CancellationToken cancellationToken)
    {
        Item? item = await store.FindAsync(ownerId, id, cancellationToken);
        List<Item> ancestors = [];
        HashSet<Guid> visited = [];
        Item? current = item;
        while (current is { Trashed: null, ParentId: { } parentId })
        {
            if (!visited.Add(current.Id) || ancestors.Count >= TreeLimits.MaximumWalkDepth)
            {
                LogBrokenAncestry(id);
                return null;
            }

            current = await store.FindAsync(ownerId, parentId, cancellationToken);
            if (current is not null)
            {
                ancestors.Insert(0, current);
            }
        }

        return item is not null && current is { Trashed: null } ? new ItemPath(item, ancestors) : null;
    }

    public async Task<ItemPath> FindActiveFolderAsync(string ownerId, string reference, CancellationToken cancellationToken)
    {
        ItemPath folder = await FindActiveAsync(ownerId, reference, cancellationToken);
        return folder.Item.IsFolder ? folder : throw NotAFolder();
    }

    public async Task<ItemPath> FindModifiableAsync(string ownerId, string reference, CancellationToken cancellationToken)
    {
        ItemPath path = await FindActiveAsync(ownerId, reference, cancellationToken);
        return path.Item.IsRoot ? throw new InvalidItemRequestException("The root folder cannot change.") : path;
    }

    public async Task<Item> FindShareableAsync(string ownerId, string reference, CancellationToken cancellationToken)
    {
        Item item = (await FindActiveAsync(ownerId, reference, cancellationToken)).Item;
        return item.IsRoot ? throw new InvalidItemRequestException("The root folder cannot be shared.") : item;
    }

    public async Task<Item> FindTrashedAsync(string ownerId, string reference, CancellationToken cancellationToken)
    {
        Item item = await FindAsync(ownerId, reference, cancellationToken);
        return item.Trashed is null ? throw NotFound(reference) : item;
    }

    private async Task<Item> FindAsync(string ownerId, string reference, CancellationToken cancellationToken)
    {
        return await store.FindAsync(ownerId, ParseId(reference), cancellationToken) ?? throw NotFound(reference);
    }

    public static Guid ParseId(string reference)
    {
        return ItemId.TryParse(reference, out Guid id)
            ? id
            : throw new InvalidItemRequestException($"The item id '{reference}' is not valid.");
    }

    public static InvalidItemRequestException NotAFolder()
    {
        return new InvalidItemRequestException("The item is not a folder.");
    }

    public static ItemNotFoundException NotFound(string reference)
    {
        return new ItemNotFoundException($"The item '{reference}' does not exist.");
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The ancestors of item {ItemId} loop back on themselves or are deeper than the limit, so the item is treated as not found.")]
    private partial void LogBrokenAncestry(Guid itemId);
}
