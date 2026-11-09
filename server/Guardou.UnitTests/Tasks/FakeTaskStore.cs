using Guardou.Application.Tasks;
using Guardou.Domain.Tasks;

namespace Guardou.UnitTests.Tasks;

internal sealed class FakeTaskStore : ITaskStore
{
    private readonly List<QueuedTask> _tasks = [];

    public int Saves { get; private set; }

    public CancellationToken LastSaveToken { get; private set; }

    public IReadOnlyList<QueuedTask> Tasks => _tasks;

    public static void Start(QueuedTask task)
    {
        typeof(QueuedTask).GetProperty(nameof(QueuedTask.State))!.SetValue(task, TaskState.Running);
    }

    public void Add(QueuedTask task)
    {
        _tasks.Add(task);
    }

    public Task<QueuedTask?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_tasks.SingleOrDefault(task => task.Id == id));
    }

    public Task<bool> ReleaseAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        QueuedTask? running = _tasks.SingleOrDefault(task => task.Id == id && task.State == TaskState.Running);
        running?.Reset(now);
        return Task.FromResult(running is not null);
    }

    public Task<int> FailAsync(TaskState state, string message, DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<QueuedTask> matching = _tasks.Where(task => task.State == state).ToList();
        matching.ForEach(task => task.Fail(message, now));
        return Task.FromResult(matching.Count);
    }

    public Task<int> ResetAsync(TaskKind? kind, TaskState state, DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<QueuedTask> matching = _tasks.Where(task => task.State == state && (kind is null || task.Kind == kind)).ToList();
        matching.ForEach(task => task.Reset(now));
        return Task.FromResult(matching.Count);
    }

    public void Remove(QueuedTask task)
    {
        _tasks.Remove(task);
    }

    public Task SaveAsync(CancellationToken cancellationToken)
    {
        Saves++;
        LastSaveToken = cancellationToken;
        return Task.CompletedTask;
    }

    public Task<TaskState?> FindStateAsync(TaskKind kind, Guid itemId, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task EnqueueAsync(
        TaskKind kind,
        IReadOnlyCollection<Guid> itemIds,
        TaskPriority priority,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<ClaimedTask?> NextPendingAsync(IReadOnlyCollection<TaskKind> kinds, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<bool> TryStartAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<IReadOnlyDictionary<TaskKind, TaskCounts>> CountAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<IReadOnlyList<TaskEntry>> ListAsync(TaskKind? kind, TaskState? state, int limit, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<TaskEntry?> FindEntryAsync(Guid id, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }
}
