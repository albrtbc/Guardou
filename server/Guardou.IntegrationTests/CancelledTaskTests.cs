using Guardou.Api.Hosting;
using Guardou.Application.Files;
using Guardou.Application.Tasks;
using Guardou.Domain.Files;
using Guardou.Domain.Tasks;
using Guardou.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Guardou.IntegrationTests;

public sealed class CancelledTaskTests : SignedInTests
{
    [Fact]
    public async Task ACancelledRunNowReturnsTheTaskToPendingInTheDatabase()
    {
        ItemEntry broken = await ItemsApi.ReadEntryAsync(await Client.UploadAsync(ItemsApi.Root, "broken.png", "not an image"));
        TaskEntry queued = await Client.WaitForTaskAsync(broken.Id, TaskState.Pending);
        Guid id = ItemId.TryParse(queued.Id, out Guid parsed) ? parsed : throw new InvalidOperationException("The task id is invalid.");
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        ITaskStore store = scope.ServiceProvider.GetRequiredService<ITaskStore>();
        QueuedTask tracked = await store.FindAsync(id, CancellationToken) ?? throw new InvalidOperationException("The task is missing.");
        Assert.True(await store.TryStartAsync(id, DateTimeOffset.UtcNow, CancellationToken));
        using CancellationTokenSource aborted = new();
        await aborted.CancelAsync();

        await scope.ServiceProvider.GetRequiredService<TaskRunner>().RunAsync(id, aborted.Token);

        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        QueuedTask stored = await context.Set<QueuedTask>().AsNoTracking().SingleAsync(task => task.Id == id, CancellationToken);
        Assert.Equal(TaskState.Pending, tracked.State);
        Assert.Equal(TaskState.Pending, stored.State);
        Assert.Null(stored.Error);
    }

    private protected override TestServerFactory CreateFactory()
    {
        return new TestServerFactory
        {
            TestServices = services => services.Remove(services.Single(descriptor => descriptor.ImplementationType == typeof(TaskWorkerPool))),
        };
    }
}
