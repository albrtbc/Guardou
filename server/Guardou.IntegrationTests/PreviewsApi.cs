using System.Net;
using System.Net.Http.Json;
using Guardou.Application.Files;
using Guardou.Application.Tasks;
using Guardou.Domain.Tasks;
using NetVips;

namespace Guardou.IntegrationTests;

internal static class PreviewsApi
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(30);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static byte[] Png(int width, int height)
    {
        using Image image = Image.Black(width, height, bands: 3);
        return image.WriteToBuffer(".png");
    }

    public static byte[] LargePng(int width, int height)
    {
        using Image image = Image.Black(width, height);
        return image.WriteToBuffer(".png", new VOption { { "compression", 1 } });
    }

    public static async Task<ItemEntry> UploadImageAsync(this HttpClient client, string fileName)
    {
        return await ItemsApi.ReadEntryAsync(await client.UploadBytesAsync(ItemsApi.Root, fileName, Png(800, 400)));
    }

    public static Task<HttpResponseMessage> GetPreviewAsync(this HttpClient client, string id, string size = "thumbnail")
    {
        return client.GetAsync($"/api/items/{id}/preview?size={size}", CancellationToken);
    }

    public static async Task<HttpResponseMessage> WaitForPreviewAsync(this HttpClient client, string id, string size = "thumbnail")
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        timeout.CancelAfter(PollTimeout);
        while (true)
        {
            HttpResponseMessage response = await client.GetPreviewAsync(id, size);
            if (response.StatusCode != HttpStatusCode.Accepted)
            {
                return response;
            }

            await Task.Delay(PollInterval, timeout.Token);
        }
    }

    public static async Task WaitForIdleQueuesAsync(this HttpClient client)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        timeout.CancelAfter(PollTimeout);
        while ((await client.ListQueuesAsync()).Any(queue => queue.Pending > 0 || queue.Running > 0))
        {
            await Task.Delay(PollInterval, timeout.Token);
        }
    }

    public static async Task<IReadOnlyList<TaskEntry>> ListTasksAsync(this HttpClient client, string query = "")
    {
        return await client.GetFromJsonAsync<IReadOnlyList<TaskEntry>>($"/api/tasks{query}", HttpClientExtensions.JsonOptions, CancellationToken)
            ?? throw new InvalidOperationException("The task list is missing.");
    }

    public static async Task<IReadOnlyList<TaskQueueSummary>> ListQueuesAsync(this HttpClient client)
    {
        return await client.GetFromJsonAsync<IReadOnlyList<TaskQueueSummary>>("/api/tasks/queues", HttpClientExtensions.JsonOptions, CancellationToken)
            ?? throw new InvalidOperationException("The queue list is missing.");
    }

    public static async Task<TaskEntry> WaitForTaskAsync(this HttpClient client, string itemId, TaskState state)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        timeout.CancelAfter(PollTimeout);
        while (true)
        {
            IReadOnlyList<TaskEntry> tasks = await client.ListTasksAsync();
            if (tasks.FirstOrDefault(task => task.ItemId == itemId && task.State == state) is { } task)
            {
                return task;
            }

            await Task.Delay(PollInterval, timeout.Token);
        }
    }
}
