using System.Collections.Concurrent;
using System.Reflection;
using Guardou.Application.Files;

namespace Guardou.IntegrationTests;

public sealed class ItemStoreProbe
{
    private static readonly TimeSpan Patience = TimeSpan.FromMilliseconds(750);

    private readonly TaskCompletionSource _bothSaving = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentQueue<string> _calls = new();
    private int _saving;

    public bool WaitForTwoSaves { get; set; }

    internal int Count(string method)
    {
        return _calls.Count(call => call == method);
    }

    internal void Record(string method)
    {
        _calls.Enqueue(method);
    }

    internal async Task ArriveAtSaveAsync()
    {
        if (!WaitForTwoSaves)
        {
            return;
        }

        if (Interlocked.Increment(ref _saving) >= 2)
        {
            _bothSaving.TrySetResult();
        }

        await Task.WhenAny(_bothSaving.Task, Task.Delay(Patience));
    }
}

public class ProbedItemStore : DispatchProxy
{
    internal IItemStore? Target { get; set; }

    internal ItemStoreProbe? Probe { get; set; }

    internal static IItemStore Wrap(IItemStore target, ItemStoreProbe probe)
    {
        IItemStore proxy = Create<IItemStore, ProbedItemStore>();
        ProbedItemStore store = (ProbedItemStore)(object)proxy;
        store.Target = target;
        store.Probe = probe;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        Probe?.Record(targetMethod?.Name ?? string.Empty);
        if (targetMethod?.Name == nameof(IItemStore.SaveAsync))
        {
            return SaveAsync((CancellationToken)(args?[0] ?? CancellationToken.None));
        }

        try
        {
            return targetMethod?.Invoke(Target, args);
        }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        {
            throw inner;
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await (Probe?.ArriveAtSaveAsync() ?? Task.CompletedTask);
        await (Target?.SaveAsync(cancellationToken) ?? Task.CompletedTask);
    }
}
