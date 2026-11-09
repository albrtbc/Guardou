using Guardou.Application.Tasks;
using Guardou.Domain.Tasks;

namespace Guardou.UnitTests.Tasks;

public sealed class TaskRunnerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private readonly FakeTaskStore _store = new();
    private readonly TaskSignal _signal = new();
    private readonly QueuedTask _task = QueuedTask.Create(TaskKind.Preview, Guid.NewGuid(), TaskPriority.Normal, Now);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ACancelledRunReturnsTheTaskToPending()
    {
        _store.Add(_task);
        FakeTaskStore.Start(_task);
        using CancellationTokenSource shutdown = new();
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskRunner runner = RunnerOf(new ScriptedHandler(async token =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
        }));

        Task run = runner.RunAsync(_task.Id, shutdown.Token);
        await started.Task.WaitAsync(CancellationToken);
        await shutdown.CancelAsync();
        await run.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);

        Assert.Equal(TaskState.Pending, _task.State);
        Assert.Null(_task.Error);
        Assert.Equal(1, _store.Saves);
        Assert.Equal(CancellationToken.None, _store.LastSaveToken);
        Assert.Contains(_task, _store.Tasks);
    }

    [Fact]
    public async Task ACancelledRunWithAnotherExceptionAlsoReturnsTheTaskToPending()
    {
        _store.Add(_task);
        FakeTaskStore.Start(_task);
        using CancellationTokenSource shutdown = new();
        TaskRunner runner = RunnerOf(new ScriptedHandler(async token =>
        {
            await shutdown.CancelAsync();
            token.ThrowIfCancellationRequested();
            throw new IOException("the pipe was closed");
        }));

        await runner.RunAsync(_task.Id, shutdown.Token);

        Assert.Equal(TaskState.Pending, _task.State);
    }

    [Fact]
    public async Task AFailedRunKeepsTheTaskWithTheErrorWhenNothingWasCancelled()
    {
        _store.Add(_task);
        FakeTaskStore.Start(_task);
        TaskRunner runner = RunnerOf(new ScriptedHandler(_ => throw new InvalidDataException("The image is too large.")));

        await runner.RunAsync(_task.Id, CancellationToken);

        Assert.Equal(TaskState.Failed, _task.State);
        Assert.Equal("The image is too large.", _task.Error);
        Assert.Contains(_task, _store.Tasks);
    }

    [Fact]
    public async Task ASuccessfulRunRemovesTheTask()
    {
        _store.Add(_task);
        FakeTaskStore.Start(_task);
        TaskRunner runner = RunnerOf(new ScriptedHandler(_ => Task.CompletedTask));

        await runner.RunAsync(_task.Id, CancellationToken);

        Assert.Empty(_store.Tasks);
        Assert.Equal(1, _store.Saves);
    }

    [Fact]
    public async Task RecoveringMarksTheTasksStillRunningAsFailed()
    {
        QueuedTask running = QueuedTask.Create(TaskKind.Preview, Guid.NewGuid(), TaskPriority.Normal, Now);
        QueuedTask pending = QueuedTask.Create(TaskKind.SearchIndex, Guid.NewGuid(), TaskPriority.Normal, Now);
        QueuedTask failed = QueuedTask.Create(TaskKind.Preview, Guid.NewGuid(), TaskPriority.Normal, Now);
        failed.Fail("The image is broken.", Now);
        FakeTaskStore.Start(running);
        _store.Add(running);
        _store.Add(pending);
        _store.Add(failed);

        await RunnerOf().RecoverAsync(CancellationToken);

        Assert.Equal(TaskState.Failed, running.State);
        Assert.Equal(QueuedTask.InterruptedError, running.Error);
        Assert.Equal(TaskState.Pending, pending.State);
        Assert.Equal("The image is broken.", failed.Error);
    }

    private TaskRunner RunnerOf(params ITaskHandler[] handlers)
    {
        return new TaskRunner(_store, handlers, _signal, TimeProvider.System);
    }

    private sealed class ScriptedHandler(Func<CancellationToken, Task> script) : ITaskHandler
    {
        public TaskKind Kind => TaskKind.Preview;

        public Task ExecuteAsync(QueuedTask task, CancellationToken cancellationToken)
        {
            return script(cancellationToken);
        }
    }
}
