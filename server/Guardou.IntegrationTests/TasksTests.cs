using System.Net;
using System.Net.Http.Json;
using Guardou.Application.Files;
using Guardou.Application.Tasks;
using Guardou.Domain.Files;
using Guardou.Domain.Tasks;
using Guardou.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Guardou.IntegrationTests;

public sealed class TasksTests : SignedInTests
{
    [Fact]
    public async Task ListsFailedTasksWithTheItemAndTheOwner()
    {
        ItemEntry broken = await UploadBrokenImageAsync();

        TaskEntry task = await Client.WaitForTaskAsync(broken.Id, TaskState.Failed);
        IReadOnlyList<TaskEntry> failed = await Client.ListTasksAsync("?kind=preview&state=failed");
        IReadOnlyList<TaskQueueSummary> queues = await Client.ListQueuesAsync();

        Assert.Equal(TaskKind.Preview, task.Kind);
        Assert.Equal("broken.png", task.ItemName);
        Assert.Equal(TestServerFactory.AdminUsername, task.Owner);
        Assert.NotNull(task.Error);
        Assert.Equal([task.Id], failed.Select(entry => entry.Id));
        Assert.Equal([new TaskQueueSummary(TaskKind.Preview, 0, 0, 1), new TaskQueueSummary(TaskKind.SearchIndex, 0, 0, 0)], queues);
    }

    [Fact]
    public async Task ResetsAFailedTask()
    {
        ItemEntry broken = await UploadBrokenImageAsync();
        TaskEntry task = await Client.WaitForTaskAsync(broken.Id, TaskState.Failed);

        HttpResponseMessage response = await Client.PostAsync($"/api/tasks/{task.Id}/reset", null, CancellationToken);
        TaskEntry? reset = await response.Content.ReadFromJsonAsync<TaskEntry>(HttpClientExtensions.JsonOptions, CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TaskState.Pending, reset?.State);
        Assert.Null(reset?.Error);
    }

    [Fact]
    public async Task RunsAFailedTaskImmediately()
    {
        ItemEntry broken = await UploadBrokenImageAsync();
        TaskEntry task = await Client.WaitForTaskAsync(broken.Id, TaskState.Failed);

        HttpResponseMessage response = await Client.PostAsync($"/api/tasks/{task.Id}/run", null, CancellationToken);
        TaskEntry? run = await response.Content.ReadFromJsonAsync<TaskEntry>(HttpClientExtensions.JsonOptions, CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TaskState.Failed, run?.State);
        Assert.True(run?.Updated >= task.Updated);
    }

    [Fact]
    public async Task FailsThePreviewOfAnImageOverThePixelLimitAndKeepsServing()
    {
        ItemEntry huge = await ItemsApi.ReadEntryAsync(
            await Client.UploadBytesAsync(ItemsApi.Root, "huge.png", PreviewsApi.LargePng(20_000, 20_000)));

        TaskEntry task = await Client.WaitForTaskAsync(huge.Id, TaskState.Failed);
        ItemEntry image = await Client.UploadImageAsync("wide.png");

        Assert.Contains("megapixels", task.Error, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await Client.WaitForPreviewAsync(image.Id)).StatusCode);
    }

    [Fact]
    public async Task TasksInterruptedByARestartFailAndAdminsCanResetThem()
    {
        TaskEntry task = await InterruptedTaskAsync();

        HttpResponseMessage response = await Client.PostAsync($"/api/tasks/{task.Id}/reset", null, CancellationToken);
        TaskEntry? reset = await response.Content.ReadFromJsonAsync<TaskEntry>(HttpClientExtensions.JsonOptions, CancellationToken);

        Assert.Equal(QueuedTask.InterruptedError, task.Error);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TaskState.Pending, reset?.State);
        Assert.Null(reset?.Error);
    }

    [Fact]
    public async Task AdminsCanRunTasksInterruptedByARestart()
    {
        TaskEntry task = await InterruptedTaskAsync();

        HttpResponseMessage response = await Client.PostAsync($"/api/tasks/{task.Id}/run", null, CancellationToken);
        TaskEntry? run = await response.Content.ReadFromJsonAsync<TaskEntry>(HttpClientExtensions.JsonOptions, CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(QueuedTask.InterruptedError, run?.Error);
    }

    [Fact]
    public async Task RefusesToResetATaskThatDidNotFail()
    {
        HttpResponseMessage missing = await Client.PostAsync("/api/tasks/AAAAAAAAAAAAAAAAAAAAAA/reset", null, CancellationToken);
        HttpResponseMessage invalid = await Client.PostAsync("/api/tasks/invalid/run", null, CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, invalid.StatusCode);
    }

    [Fact]
    public async Task RegeneratesAllPreviews()
    {
        ItemEntry image = await Client.UploadImageAsync("wide.png");
        await Client.WaitForPreviewAsync(image.Id);

        HttpResponseMessage response = await Client.PostAsync("/api/tasks/queues/preview/all", null, CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.WaitForPreviewAsync(image.Id)).StatusCode);
    }

    [Fact]
    public async Task ResetsTheFailedTasksOfAQueue()
    {
        ItemEntry broken = await UploadBrokenImageAsync();
        await Client.WaitForTaskAsync(broken.Id, TaskState.Failed);

        HttpResponseMessage response = await Client.PostAsync("/api/tasks/queues/preview/reset", null, CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ListsAllQueues()
    {
        Assert.Equal(
            [new TaskQueueSummary(TaskKind.Preview, 0, 0, 0), new TaskQueueSummary(TaskKind.SearchIndex, 0, 0, 0)],
            await Client.ListQueuesAsync());
    }

    [Fact]
    public async Task RefusesAnUnknownQueue()
    {
        HttpResponseMessage response = await Client.PostAsync("/api/tasks/queues/unknown/all", null, CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RefusesUsersWithoutTheAdminType()
    {
        using HttpClient bob = await SignInAsBobAsync();

        HttpResponseMessage list = await bob.GetAsync("/api/tasks", CancellationToken);
        HttpResponseMessage regenerate = await bob.PostAsync("/api/tasks/queues/preview/all", null, CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, regenerate.StatusCode);
    }

    [Fact]
    public async Task RefusesAnInvalidLimit()
    {
        HttpResponseMessage response = await Client.GetAsync("/api/tasks?limit=0", CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<TaskEntry> InterruptedTaskAsync()
    {
        ItemEntry broken = await UploadBrokenImageAsync();
        TaskEntry failed = await Client.WaitForTaskAsync(broken.Id, TaskState.Failed);
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Guid id = ItemId.TryParse(failed.Id, out Guid parsed) ? parsed : throw new InvalidOperationException("The task id is invalid.");
        await context.Set<QueuedTask>()
            .Where(task => task.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(task => task.State, TaskState.Running), CancellationToken);
        await scope.ServiceProvider.GetRequiredService<TaskRunner>().RecoverAsync(CancellationToken);
        return await Client.WaitForTaskAsync(broken.Id, TaskState.Failed);
    }

    private async Task<ItemEntry> UploadBrokenImageAsync()
    {
        return await ItemsApi.ReadEntryAsync(await Client.UploadAsync(ItemsApi.Root, "broken.png", "not an image"));
    }
}
