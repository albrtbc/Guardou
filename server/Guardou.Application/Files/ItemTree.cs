using Guardou.Domain.Files;
using Microsoft.Extensions.Logging;

namespace Guardou.Application.Files;

internal sealed partial class ItemTree(IItemStore store, ILogger<ItemTree> logger)
{
    public async Task<IReadOnlyList<Item>> SubtreeAsync(Item item, CancellationToken cancellationToken)
    {
        List<TreeNode> nodes = await WalkAsync(item, path: null, includeTrashed: true, limit: int.MaxValue, maxDepth: int.MaxValue, cancellationToken);
        return nodes.Select(node => node.Item).ToList();
    }

    public async Task<IReadOnlyList<TreeNode>> ActiveSubtreeAsync(Item item, string path, CancellationToken cancellationToken)
    {
        return await WalkAsync(item, path, includeTrashed: false, limit: int.MaxValue, TreeLimits.MaximumWalkDepth, cancellationToken);
    }

    public async Task<IReadOnlyList<TreeNode>> ActiveSubtreeAsync(Item item, string path, int limit, CancellationToken cancellationToken)
    {
        return await WalkAsync(item, path, includeTrashed: false, limit, TreeLimits.MaximumWalkDepth, cancellationToken);
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

    private async Task<List<TreeNode>> WalkAsync(Item root, string? path, bool includeTrashed, int limit, int maxDepth, CancellationToken cancellationToken)
    {
        List<TreeNode> nodes = [new TreeNode(root, path ?? string.Empty, 0)];
        HashSet<Guid> visited = [root.Id];
        bool reported = false;
        for (int index = 0; index < nodes.Count && nodes.Count <= limit; index++)
        {
            TreeNode node = nodes[index];
            if (!node.Item.IsFolder)
            {
                continue;
            }

            IReadOnlyList<Item> children = includeTrashed
                ? await store.ChildrenWithTrashAsync(node.Item.Id, cancellationToken)
                : await store.ChildrenAsync(node.Item.Id, cancellationToken);
            foreach (Item child in children)
            {
                if (node.Depth >= maxDepth || !visited.Add(child.Id))
                {
                    if (!reported)
                    {
                        LogBrokenTree(child.Id);
                        reported = true;
                    }

                    continue;
                }

                nodes.Add(new TreeNode(child, path is null ? string.Empty : $"{node.Path}/{child.Name}", node.Depth + 1));
            }
        }

        return nodes;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Item {ItemId} sits in a folder tree that loops back on itself or is deeper than the limit, so the walk skips it.")]
    private partial void LogBrokenTree(Guid itemId);
}

internal sealed record TreeNode(Item Item, string Path, int Depth);
