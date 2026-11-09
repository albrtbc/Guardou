using Guardou.Domain.Tasks;

namespace Guardou.Application.Tasks;

public sealed class TaskRunner
{
    private readonly ITaskStore _store;
    private readonly Dictionary<TaskKind, ITaskHandler> _handlers;
    private readonly TaskSignal _signal;
    private readonly TimeProvider _time;

    internal TaskRunner(ITaskStore store, IEnumerable<ITaskHandler> handlers, TaskSignal signal, TimeProvider time)
    {
        _store = store;
        _handlers = handlers.ToDictionary(handler => handler.Kind);
        _signal = signal;
        _time = time;
    }

    public async Task<ClaimedTask?> ClaimNextAsync(IReadOnlyCollection<TaskKind> kinds, CancellationToken cancellationToken)
    {
        while (await _store.NextPendingAsync(kinds, cancellationToken) is { } task)
        {
            if (await _store.TryStartAsync(task.Id, _time.GetUtcNow(), cancellationToken))
            {
                return task;
            }
        }

        return null;
    }

    public async Task RunAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await _store.FindAsync(id, CancellationToken.None) is not { } task)
        {
            return;
        }

        try
        {
            await _handlers[task.Kind].ExecuteAsync(task, cancellationToken);
            _store.Remove(task);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            await _store.ReleaseAsync(task.Id, _time.GetUtcNow(), CancellationToken.None);
            _signal.Notify();
        }
        catch (Exception exception)
        {
            task.Fail(exception.Message, _time.GetUtcNow());
        }

        await _store.SaveAsync(CancellationToken.None);
    }

    public Task RecoverAsync(CancellationToken cancellationToken)
    {
        return _store.FailAsync(TaskState.Running, QueuedTask.InterruptedError, _time.GetUtcNow(), cancellationToken);
    }
}
