using Guardou.Domain.Files;
using Guardou.Infrastructure.Indexing;
using Microsoft.EntityFrameworkCore;

namespace Guardou.Infrastructure.Persistence;

internal sealed class ItemNameNormalizer(AppDbContext context, ItemIndexWriter index)
{
    public const string Migration = "NormalizeItemNames";

    private const string NonAsciiPattern = "*[^ -~]*";

    private DbSet<Item> Items => context.Set<Item>();

    public async Task NormalizeAsync(CancellationToken cancellationToken)
    {
        List<Item> candidates = await Items.Where(item => EF.Functions.Glob(item.Name, NonAsciiPattern)).ToListAsync(cancellationToken);
        foreach (Item item in candidates)
        {
            if (ItemName.TryNormalize(item.Name) is { } normalized && normalized != item.Name)
            {
                item.ReplaceName(item.Trashed is null ? await FreeNameAsync(item, normalized, cancellationToken) : normalized);
                await index.SaveAsync(cancellationToken);
            }
        }
    }

    private async Task<string> FreeNameAsync(Item item, string name, CancellationToken cancellationToken)
    {
        string candidate = name;
        for (int number = 1; await IsTakenAsync(item, candidate, cancellationToken); number++)
        {
            candidate = ItemName.Numbered(name, number);
        }

        return candidate;
    }

    private Task<bool> IsTakenAsync(Item item, string name, CancellationToken cancellationToken)
    {
        return Items.AnyAsync(
            other => other.ParentId == item.ParentId && other.Trashed == null && other.Name == name && other.Id != item.Id,
            cancellationToken);
    }
}
