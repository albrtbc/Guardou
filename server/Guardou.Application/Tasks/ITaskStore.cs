using Guardou.Domain.Tasks;

namespace Guardou.Application.Tasks;

public interface ITaskStore
{
    Task<QueuedTask?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<TaskState?> FindStateAsync(TaskKind kind, Guid itemId, CancellationToken cancellationToken);

    Task EnqueueAsync(
        TaskKind kind,
        IReadOnlyCollection<Guid> itemIds,
        TaskPriority priority,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<ClaimedTask?> NextPendingAsync(IReadOnlyCollection<TaskKind> kinds, CancellationToken cancellationToken);

    Task<bool> TryStartAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken);

    Task<int> ResetAsync(TaskKind? kind, TaskState state, DateTimeOffset now, CancellationToken cancellationToken);

    Task<bool> ReleaseAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken);

    Task<int> FailAsync(TaskState state, string message, DateTimeOffset now, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<TaskKind, TaskCounts>> CountAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskEntry>> ListAsync(TaskKind? kind, TaskState? state, int limit, CancellationToken cancellationToken);

    Task<TaskEntry?> FindEntryAsync(Guid id, CancellationToken cancellationToken);

    void Remove(QueuedTask task);

    Task SaveAsync(CancellationToken cancellationToken);
}
