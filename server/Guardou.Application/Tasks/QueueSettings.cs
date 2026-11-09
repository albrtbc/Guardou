using Guardou.Application.Audit;
using Guardou.Application.Settings;
using Guardou.Domain.Tasks;

namespace Guardou.Application.Tasks;

public sealed class QueueSettings
{
    private readonly ITaskQueueSettingsStore _store;
    private readonly IEnumerable<ITaskProducer> _producers;
    private readonly TaskSignal _signal;
    private readonly AuditLog _audit;

    internal QueueSettings(ITaskQueueSettingsStore store, IEnumerable<ITaskProducer> producers, TaskSignal signal, AuditLog audit)
    {
        _store = store;
        _producers = producers;
        _signal = signal;
        _audit = audit;
    }

    public async Task<IReadOnlyList<TaskConcurrency>> ConcurrencyAsync(CancellationToken cancellationToken)
    {
        List<TaskConcurrency> limits = [];
        foreach (TaskKind kind in Kinds())
        {
            TaskQueueSettings settings = await _store.FindAsync(kind, cancellationToken) ?? TaskQueueSettings.Create(kind);
            limits.Add(new TaskConcurrency(kind, settings.Concurrency));
        }

        return limits;
    }

    public IReadOnlyList<TaskConcurrency> DefaultConcurrency()
    {
        return Kinds().Select(kind => new TaskConcurrency(kind, TaskQueueSettings.DefaultConcurrency)).ToList();
    }

    public async Task ChangeConcurrencyAsync(IReadOnlyList<TaskConcurrency> limits, CancellationToken cancellationToken)
    {
        foreach (TaskConcurrency limit in limits)
        {
            if (!Kinds().Contains(limit.Kind) || !TaskQueueSettings.IsValidConcurrency(limit.Concurrency))
            {
                throw new InvalidSettingsException($"The concurrency {limit.Concurrency} of the queue '{limit.Kind}' is not valid.");
            }

            (await FindOrCreateAsync(limit.Kind, cancellationToken)).ChangeConcurrency(limit.Concurrency);
        }

        await _store.SaveAsync(cancellationToken);
        _audit.SettingsChanged(AuditSections.Tasks);
        _signal.Notify();
    }

    private IEnumerable<TaskKind> Kinds()
    {
        return _producers.Select(producer => producer.Kind).Order();
    }

    private async Task<TaskQueueSettings> FindOrCreateAsync(TaskKind kind, CancellationToken cancellationToken)
    {
        if (await _store.FindAsync(kind, cancellationToken) is { } settings)
        {
            return settings;
        }

        TaskQueueSettings created = TaskQueueSettings.Create(kind);
        _store.Add(created);
        return created;
    }
}
