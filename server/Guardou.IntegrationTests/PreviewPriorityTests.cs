using System.Net;
using System.Net.Http.Json;
using Guardou.Api.Hosting;
using Guardou.Application.Files;
using Guardou.Application.Shares;
using Guardou.Application.Tasks;
using Guardou.Domain.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Guardou.IntegrationTests;

public sealed class PreviewPriorityTests : SignedInTests
{
    [Fact]
    public async Task OwnerPreviewRequestsAddHighTasks()
    {
        ItemEntry image = await UploadWithoutTaskAsync();

        HttpResponseMessage response = await Client.GetPreviewAsync(image.Id);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(TaskPriority.High, await PriorityOfAsync(image.Id));
    }

    [Fact]
    public async Task AnonymousPreviewRequestsAddNormalTasks()
    {
        ItemEntry image = await UploadWithoutTaskAsync();
        HttpResponseMessage shared = await Client.PutAsync($"/api/items/{image.Id}/share", null, CancellationToken);
        ShareLink? link = await shared.Content.ReadFromJsonAsync<ShareLink>(HttpClientExtensions.JsonOptions, CancellationToken);
        using HttpClient anonymous = Factory.CreateClient();

        HttpResponseMessage response = await anonymous.GetAsync(
            $"/api/shares/{link?.Url.Split('/')[^1]}/items/{image.Id}/preview?size=thumbnail",
            CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(TaskPriority.Normal, await PriorityOfAsync(image.Id));
    }

    private async Task<ItemEntry> UploadWithoutTaskAsync()
    {
        ItemEntry image = await Client.UploadImageAsync("photo.png");
        TaskEntry task = Assert.Single(await Client.ListTasksAsync());
        (await Client.PostAsync($"/api/tasks/{task.Id}/run", null, CancellationToken)).EnsureSuccessStatusCode();
        Directory.Delete(Factory.ThumbsDirectory, recursive: true);
        return image;
    }

    private async Task<TaskPriority> PriorityOfAsync(string itemId)
    {
        return Assert.Single(await Client.ListTasksAsync(), task => task.ItemId == itemId).Priority;
    }

    private protected override TestServerFactory CreateFactory()
    {
        return new TestServerFactory
        {
            TestServices = services =>
            {
                ServiceDescriptor workers = services.Single(descriptor =>
                    descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(TaskWorkerPool));
                services.Remove(workers);
            },
        };
    }
}
