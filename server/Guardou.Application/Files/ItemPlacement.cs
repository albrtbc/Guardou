using Guardou.Application.Shares;
using Guardou.Domain.Files;

namespace Guardou.Application.Files;

internal sealed class ItemPlacement(IItemStore store, ShareCleanup shares, TimeProvider time)
{
    public const int MaximumNumberedNames = 10_000;

    public async Task<string?> ResolveNameAsync(
        Item folder,
        string name,
        ConflictPolicy policy,
        ItemPath? source,
        CancellationToken cancellationToken)
    {
        Item? existing = await store.FindChildAsync(folder.Id, name, cancellationToken);
        if (existing is null)
        {
            return name;
        }

        return policy switch
        {
            ConflictPolicy.Skip => null,
            ConflictPolicy.Rename => await FreeNameAsync(folder, name, cancellationToken),
            ConflictPolicy.Replace when source is null || !source.Contains(existing.Id) => await ReplaceAsync(existing, cancellationToken),
            _ => throw ItemConflictException.ForName(name),
        };
    }

    public static string ValidName(string name)
    {
        return ItemName.TryNormalize(name) is { } normalized && ItemName.IsValid(normalized)
            ? normalized
            : throw new InvalidItemRequestException($"The name '{name}' is not valid.");
    }

    private async Task<string> FreeNameAsync(Item folder, string name, CancellationToken cancellationToken)
    {
        for (int number = 1; number <= MaximumNumberedNames; number++)
        {
            string candidate = ItemName.Numbered(name, number);
            if (await store.FindChildAsync(folder.Id, candidate, cancellationToken) is null)
            {
                return candidate;
            }
        }

        throw new ItemConflictException($"No free name for '{name}' was found after {MaximumNumberedNames} attempts. Rename the item first.");
    }

    private async Task<string> ReplaceAsync(Item existing, CancellationToken cancellationToken)
    {
        existing.Trash(time.GetUtcNow());
        await store.SaveAsync(cancellationToken);
        await shares.RemoveInactiveAsync(existing.OwnerId, cancellationToken);
        return existing.Name;
    }
}
