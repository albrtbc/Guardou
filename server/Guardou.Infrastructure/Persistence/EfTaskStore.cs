using Guardou.Application.Tasks;
using Guardou.Domain.Files;
using Guardou.Domain.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Guardou.Infrastructure.Persistence;

internal sealed class EfTaskStore(AppDbContext context) : ITaskStore
{
    private const int ConstraintError = 19;

    private DbSet<QueuedTask> Tasks => context.Set<QueuedTask>();

    public Task<QueuedTask?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return Tasks.SingleOrDefaultAsync(task => task.Id == id, cancellationToken);
    }

    public Task<TaskState?> FindStateAsync(TaskKind kind, Guid itemId, CancellationToken cancellationToken)
    {
        return Tasks
            .Where(task => task.Kind == kind && task.ItemId == itemId)
            .Select(task => (TaskState?)task.State)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task EnqueueAsync(
        TaskKind kind,
        IReadOnlyCollection<Guid> itemIds,
        TaskPriority priority,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        IQueryable<QueuedTask> existing = Tasks.Where(task => task.Kind == kind && itemIds.Contains(task.ItemId));
        await existing
            .Where(task => task.Priority < priority)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(task => task.Priority, priority)
                    .SetProperty(task => task.Updated, now),
                cancellationToken);
        HashSet<Guid> queued = [.. await existing.Select(task => task.ItemId).ToListAsync(cancellationToken)];
        List<QueuedTask> added = itemIds
            .Where(itemId => !queued.Contains(itemId))
            .Select(itemId => QueuedTask.Create(kind, itemId, priority, now))
            .ToList();
        Tasks.AddRange(added);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteErrorCode: ConstraintError })
        {
            added.ForEach(task => context.Entry(task).State = EntityState.Detached);
        }
    }

    public Task<ClaimedTask?> NextPendingAsync(IReadOnlyCollection<TaskKind> kinds, CancellationToken cancellationToken)
    {
        return Tasks
            .Where(task => task.State == TaskState.Pending && kinds.Contains(task.Kind))
            .OrderByDescending(task => task.Priority)
            .ThenBy(task => task.Created)
            .Select(task => new ClaimedTask(task.Id, task.Kind))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> TryStartAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        int started = await Tasks
            .Where(task => task.Id == id && task.State == TaskState.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(task => task.State, TaskState.Running)
                    .SetProperty(task => task.Updated, now),
                cancellationToken);
        return started == 1;
    }

    public Task<int> ResetAsync(TaskKind? kind, TaskState state, DateTimeOffset now, CancellationToken cancellationToken)
    {
        return Tasks
            .Where(task => task.State == state && (kind == null || task.Kind == kind))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(task => task.State, TaskState.Pending)
                    .SetProperty(task => task.Error, (string?)null)
                    .SetProperty(task => task.Updated, now),
                cancellationToken);
    }

    public async Task<bool> ReleaseAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        int released = await Tasks
            .Where(task => task.Id == id && task.State == TaskState.Running)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(task => task.State, TaskState.Pending)
                    .SetProperty(task => task.Error, (string?)null)
                    .SetProperty(task => task.Updated, now),
                cancellationToken);
        return released == 1;
    }

    public Task<int> FailAsync(TaskState state, string message, DateTimeOffset now, CancellationToken cancellationToken)
    {
        return Tasks
            .Where(task => task.State == state)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(task => task.State, TaskState.Failed)
                    .SetProperty(task => task.Error, message)
                    .SetProperty(task => task.Updated, now),
                cancellationToken);
    }

    public async Task<IReadOnlyDictionary<TaskKind, TaskCounts>> CountAsync(CancellationToken cancellationToken)
    {
        var counts = await Tasks
            .GroupBy(task => new { task.Kind, task.State })
            .Select(group => new { group.Key.Kind, group.Key.State, Count = group.Count() })
            .ToListAsync(cancellationToken);
        return counts
            .GroupBy(count => count.Kind)
            .ToDictionary(
                kind => kind.Key,
                kind => new TaskCounts(
                    kind.Where(count => count.State == TaskState.Pending).Sum(count => count.Count),
                    kind.Where(count => count.State == TaskState.Running).Sum(count => count.Count),
                    kind.Where(count => count.State == TaskState.Failed).Sum(count => count.Count)));
    }

    public async Task<IReadOnlyList<TaskEntry>> ListAsync(TaskKind? kind, TaskState? state, int limit, CancellationToken cancellationToken)
    {
        List<TaskRow> rows = await Rows()
            .Where(row => (kind == null || row.Task.Kind == kind) && (state == null || row.Task.State == state))
            .OrderBy(row => row.Task.State == TaskState.Running ? 0 : 1)
            .ThenByDescending(row => row.Task.Priority)
            .ThenBy(row => row.Task.Created)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return rows.Select(ToEntry).ToList();
    }

    public async Task<TaskEntry?> FindEntryAsync(Guid id, CancellationToken cancellationToken)
    {
        TaskRow? row = await Rows().SingleOrDefaultAsync(row => row.Task.Id == id, cancellationToken);
        return row is null ? null : ToEntry(row);
    }

    public void Remove(QueuedTask task)
    {
        Tasks.Remove(task);
    }

    public Task SaveAsync(CancellationToken cancellationToken)
    {
        return context.SaveChangesAsync(cancellationToken);
    }

    private static TaskEntry ToEntry(TaskRow row)
    {
        QueuedTask task = row.Task;
        return new TaskEntry(
            ItemId.Format(task.Id),
            task.Kind,
            task.State,
            task.Priority,
            ItemId.Format(task.ItemId),
            row.ItemName,
            row.Owner ?? string.Empty,
            task.Error,
            task.Created,
            task.Updated);
    }

    private IQueryable<TaskRow> Rows()
    {
        return from task in Tasks.AsNoTracking()
               join item in context.Set<Item>() on task.ItemId equals item.Id
               join user in context.Users on item.OwnerId equals user.Id
               select new TaskRow { Task = task, ItemName = item.Name, Owner = user.UserName };
    }

    private sealed class TaskRow
    {
        public required QueuedTask Task { get; init; }

        public required string ItemName { get; init; }

        public required string? Owner { get; init; }
    }
}
