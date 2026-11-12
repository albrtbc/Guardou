using Guardou.Domain.Files;
using Microsoft.Extensions.Logging;

namespace Guardou.Application.Files;

internal sealed partial class ItemTree(IItemStore store, ILogger<ItemTree> logger)
{
    public async Task<IReadOnlyList<Item>> SubtreeAsync(Item item, CancellationToken cancellationToken)
    {
        IReadOnlyList<Item> items = await store.SubtreeAsync(item.Id, cancellationToken);
        if (LoopsBack(item, items))
        {
            LogBrokenTree(item.Id);
        }

        return items;
    }

    public async Task<IReadOnlyList<TreeNode>> ActiveSubtreeAsync(Item item, string path, CancellationToken cancellationToken)
    {
        return Nodes(item, path, await store.ActiveSubtreeAsync(item.Id, cancellationToken));
    }

    public async Task<IReadOnlyList<TreeNode>> ActiveSubtreeAsync(Item item, string path, int limit, CancellationToken cancellationToken)
    {
        return Nodes(item, path, await store.ActiveSubtreeAsync(item.Id, limit + 1, cancellationToken));
    }

    public async Task<int> FolderHeightAsync(Item item, int maxLevels, CancellationToken cancellationToken)
    {
        if (!item.IsFolder)
        {
            return -1;
        }

        HashSet<Guid> visited = [item.Id];
        List<Guid> level = [item.Id];
        int height = 0;
        while (height < maxLevels)
        {
            level = (await store.ChildFolderIdsAsync(level, cancellationToken)).Where(visited.Add).ToList();
            if (level.Count == 0)
            {
                break;
            }

            height++;
        }

        return height;
    }

    public static int DeepestFolderLevel(IEnumerable<TreeNode> nodes)
    {
        return nodes.Where(node => node.Item.IsFolder).Select(node => node.Depth).DefaultIfEmpty(-1).Max();
    }

    private static bool LoopsBack(Item root, IReadOnlyList<Item> items)
    {
        return root.ParentId is { } parentId && items.Any(item => item.Id == parentId);
    }

    private List<TreeNode> Nodes(Item root, string path, IReadOnlyList<Item> items)
    {
        Dictionary<Guid, TreeNode> placed = [];
        Dictionary<Guid, CaseInsensitiveNames> names = [];
        List<TreeNode> nodes = [];
        bool broken = LoopsBack(root, items);
        foreach (Item current in items)
        {
            TreeNode node;
            if (current.Id == root.Id)
            {
                node = new TreeNode(current, path, 0);
            }
            else if (current.ParentId is { } parentId && placed.TryGetValue(parentId, out TreeNode? parent) && parent.Depth < TreeLimits.MaximumWalkDepth)
            {
                node = new TreeNode(current, $"{parent.Path}/{NamesIn(names, parentId).Claim(current.Name)}", parent.Depth + 1);
            }
            else
            {
                broken = true;
                continue;
            }

            placed[current.Id] = node;
            nodes.Add(node);
        }

        if (broken)
        {
            LogBrokenTree(root.Id);
        }

        return nodes;
    }

    private static CaseInsensitiveNames NamesIn(Dictionary<Guid, CaseInsensitiveNames> names, Guid parentId)
    {
        if (!names.TryGetValue(parentId, out CaseInsensitiveNames? folder))
        {
            folder = new CaseInsensitiveNames();
            names[parentId] = folder;
        }

        return folder;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Item {ItemId} sits in a folder tree that loops back on itself or is deeper than the limit, so the walk skips it.")]
    private partial void LogBrokenTree(Guid itemId);
}

internal sealed record TreeNode(Item Item, string Path, int Depth);
