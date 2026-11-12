using Guardou.Application.Files;
using Guardou.Application.Tasks;
using Guardou.Domain.Files;
using Guardou.Domain.Previews;
using Guardou.Domain.Tasks;

namespace Guardou.Application.Previews;

public sealed class ItemPreviews
{
    private readonly AccessibleItems _items;
    private readonly ITaskStore _tasks;
    private readonly IPreviewRenderer _renderer;
    private readonly IPreviewCache _cache;
    private readonly PreviewTasks _previewTasks;

    internal ItemPreviews(AccessibleItems items, ITaskStore tasks, IPreviewRenderer renderer, IPreviewCache cache, PreviewTasks previewTasks)
    {
        _items = items;
        _tasks = tasks;
        _renderer = renderer;
        _cache = cache;
        _previewTasks = previewTasks;
    }

    public Task<Stream?> OpenAsync(string userId, string id, PreviewSize size, CancellationToken cancellationToken)
    {
        return OpenAsync(userId, id, size, TaskPriority.High, cancellationToken);
    }

    internal Task<Stream?> OpenAnonymousAsync(string ownerId, string id, PreviewSize size, CancellationToken cancellationToken)
    {
        return OpenAsync(ownerId, id, size, TaskPriority.Normal, cancellationToken);
    }

    private async Task<Stream?> OpenAsync(
        string userId,
        string id,
        PreviewSize size,
        TaskPriority priority,
        CancellationToken cancellationToken)
    {
        Item item = await FindPreviewableAsync(userId, id, cancellationToken);
        if (_cache.TryOpen(item.OwnerId, item.Id, size) is { } content)
        {
            return content;
        }

        if (await _tasks.FindStateAsync(TaskKind.Preview, item.Id, cancellationToken) == TaskState.Failed)
        {
            throw new ItemNotFoundException($"The preview of the item '{id}' failed.");
        }

        await _previewTasks.EnqueueAsync([item], priority, cancellationToken);
        return null;
    }

    private async Task<Item> FindPreviewableAsync(string userId, string id, CancellationToken cancellationToken)
    {
        Item item = (await _items.FindActiveAsync(userId, id, cancellationToken)).Item;
        return item.IsFolder || !_renderer.Supports(item.Name)
            ? throw new ItemNotFoundException($"The item '{id}' has no preview.")
            : item;
    }
}
