namespace Guardou.Domain.Tasks;

public sealed class QueuedTask
{
    public const int MaximumErrorLength = 2000;
    public const string InterruptedError = "Interrupted: the server stopped while this task was running";

    private QueuedTask(Guid id, TaskKind kind, Guid itemId, TaskPriority priority, DateTimeOffset created)
    {
        Id = id;
        Kind = kind;
        ItemId = itemId;
        Priority = priority;
        State = TaskState.Pending;
        Created = created;
        Updated = created;
    }

    public Guid Id { get; private set; }

    public TaskKind Kind { get; private set; }

    public Guid ItemId { get; private set; }

    public TaskPriority Priority { get; private set; }

    public TaskState State { get; private set; }

    public string? Error { get; private set; }

    public DateTimeOffset Created { get; private set; }

    public DateTimeOffset Updated { get; private set; }

    public static QueuedTask Create(TaskKind kind, Guid itemId, TaskPriority priority, DateTimeOffset now)
    {
        return new QueuedTask(Guid.CreateVersion7(now), kind, itemId, priority, now);
    }

    public void Fail(string error, DateTimeOffset now)
    {
        State = TaskState.Failed;
        Error = error.Length > MaximumErrorLength ? error[..MaximumErrorLength] : error;
        Updated = now;
    }

    public void Reset(DateTimeOffset now)
    {
        State = TaskState.Pending;
        Error = null;
        Updated = now;
    }
}
