using System.Net;
using System.Net.Http.Json;
using Guardou.Api.Models;
using Guardou.Application.Files;
using Guardou.Application.Quotas;
using Guardou.Application.Settings;
using Guardou.Application.Uploads;
using Guardou.Domain.Files;
using Guardou.Domain.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Guardou.IntegrationTests;

public sealed class StorageLimitsTests : SignedInTests
{
    private const long Megabyte = 1024 * 1024;

    [Fact]
    public async Task UploadBeyondTheMaximumFileSizeGives413()
    {
        await SetMaxFileSizeAsync(1);

        HttpResponseMessage refused = await Client.StartUploadAsync(ItemsApi.Root, "large.bin", Megabyte + 1);
        HttpResponseMessage accepted = await Client.StartUploadAsync(ItemsApi.Root, "exact.bin", Megabyte);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, refused.StatusCode);
        Assert.Equal([FileTooLargeException.Code], await UsersApi.ReadErrorCodesAsync(refused));
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task HasNoMaximumFileSizeByDefault()
    {
        HttpResponseMessage response = await Client.StartUploadAsync(ItemsApi.Root, "huge.bin", 10_000 * Megabyte);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task UploadIntoTheFreeSpaceReserveGives507()
    {
        Factory.Capacity.LeaveRoomFor(4);

        HttpResponseMessage refused = await Client.StartUploadAsync(ItemsApi.Root, "large.bin", 5);
        HttpResponseMessage accepted = await Client.StartUploadAsync(ItemsApi.Root, "small.bin", 4);

        Assert.Equal(HttpStatusCode.InsufficientStorage, refused.StatusCode);
        Assert.Equal([InsufficientStorageException.Code], await UsersApi.ReadErrorCodesAsync(refused));
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task ExplainsAFullServer()
    {
        Factory.Capacity.LeaveRoomFor(0);

        HttpResponseMessage response = await Client.StartUploadAsync(ItemsApi.Root, "large.bin", 5);

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken);
        Assert.Equal(new InsufficientStorageException().Message, problem?.Detail);
    }

    [Fact]
    public async Task UnfinishedUploadsOfEveryUserCountAgainstTheFreeSpace()
    {
        Factory.Capacity.LeaveRoomFor(4);
        UploadSession first = await Client.StartUploadSessionAsync(ItemsApi.Root, "first.bin", 4);
        HttpClient bob = await SignInAsBobAsync();

        HttpResponseMessage refused = await bob.StartUploadAsync(ItemsApi.Root, "second.bin", 1);
        (await Client.CancelUploadAsync(first.Id)).EnsureSuccessStatusCode();
        HttpResponseMessage accepted = await bob.StartUploadAsync(ItemsApi.Root, "second.bin", 1);

        Assert.Equal(HttpStatusCode.InsufficientStorage, refused.StatusCode);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task OnlyTheBytesStillToWriteCountAsPending()
    {
        Factory.Capacity.LeaveRoomFor(10);
        UploadSession session = await Client.StartUploadSessionAsync(ItemsApi.Root, "first.bin", 6);
        (await Client.WriteUploadAsync(session.Id, 0, new byte[6])).EnsureSuccessStatusCode();
        Factory.Capacity.Available -= 6;

        HttpResponseMessage accepted = await Client.StartUploadAsync(ItemsApi.Root, "second.bin", 4);
        HttpResponseMessage refused = await Client.StartUploadAsync(ItemsApi.Root, "third.bin", 1);

        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.InsufficientStorage, refused.StatusCode);
    }

    [Fact]
    public async Task CopiesInProgressOfAnotherUserCountAgainstTheFreeSpace()
    {
        string adminId = (await Client.FindUserAsync(TestServerFactory.AdminUsername)).Id;
        string bobId = (await Client.FindUserAsync(BobUsername)).Id;
        Factory.Capacity.LeaveRoomFor(10);
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource finish = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task first = ReserveAsync(adminId, 8, async () =>
        {
            started.SetResult();
            await finish.Task;
        });
        await started.Task;
        await Assert.ThrowsAsync<InsufficientStorageException>(() => ReserveAsync(bobId, 8, () => Task.CompletedTask));
        finish.SetResult();
        await first;
        await ReserveAsync(bobId, 8, () => Task.CompletedTask);
    }

    [Fact]
    public async Task CopyIntoTheFreeSpaceReserveGives507()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "a.txt", "hello");
        ItemEntry folder = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        IReadOnlyList<string> blobs = BlobFiles();
        Factory.Capacity.LeaveRoomFor(4);

        HttpResponseMessage refused = await Client.TransferAsync("copy", file.Id, folder.Id);
        Factory.Capacity.LeaveRoomFor(5);
        HttpResponseMessage accepted = await Client.TransferAsync("copy", file.Id, folder.Id);

        Assert.Equal(HttpStatusCode.InsufficientStorage, refused.StatusCode);
        Assert.Equal([InsufficientStorageException.Code], await UsersApi.ReadErrorCodesAsync(refused));
        Assert.Equal(blobs.Count + 1, BlobFiles().Count);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task ChangesTheFreeSpaceReserve()
    {
        Factory.Capacity.Available = 10 * Megabyte;
        Assert.Equal(
            HttpStatusCode.InsufficientStorage,
            (await Client.StartUploadAsync(ItemsApi.Root, "a.bin", 1)).StatusCode);

        await SetStorageLimitsAsync(maxFileSizeMb: null, minFreeSpaceMb: 5);

        Assert.Equal(HttpStatusCode.Created, (await Client.StartUploadAsync(ItemsApi.Root, "a.bin", 1)).StatusCode);
        Assert.Equal(
            HttpStatusCode.InsufficientStorage,
            (await Client.StartUploadAsync(ItemsApi.Root, "b.bin", 6 * Megabyte)).StatusCode);
    }

    [Fact]
    public async Task WriteIntoTheFreeSpaceReserveGives507()
    {
        UploadSession session = await Client.StartUploadSessionAsync(ItemsApi.Root, "a.txt", 5);
        Factory.Capacity.LeaveRoomFor(4);

        HttpResponseMessage refused = await Client.WriteUploadAsync(session.Id, 0, "hello"u8.ToArray());
        HttpResponseMessage info = await Client.GetUploadInfoAsync(session.Id);
        Factory.Capacity.LeaveRoomFor(5);
        HttpResponseMessage accepted = await Client.WriteUploadAsync(session.Id, 0, "hello"u8.ToArray());

        Assert.Equal(HttpStatusCode.InsufficientStorage, refused.StatusCode);
        Assert.Equal(0, UploadsApi.OffsetOf(info));
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
    }

    [Fact]
    public async Task TheTwentyFirstUploadInProgressGives429()
    {
        List<UploadSession> sessions = [];
        for (int index = 0; index < StoragePolicy.MaximumUploadsInProgress; index++)
        {
            sessions.Add(await Client.StartUploadSessionAsync(ItemsApi.Root, $"file{index}.bin", 1));
        }

        HttpResponseMessage refused = await Client.StartUploadAsync(ItemsApi.Root, "extra.bin", 1);
        HttpResponseMessage other = await (await SignInAsBobAsync()).StartUploadAsync(ItemsApi.Root, "bob.bin", 1);
        (await Client.CancelUploadAsync(sessions[0].Id)).EnsureSuccessStatusCode();
        HttpResponseMessage accepted = await Client.StartUploadAsync(ItemsApi.Root, "extra.bin", 1);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal([TooManyUploadsException.Code], await UsersApi.ReadErrorCodesAsync(refused));
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task ParallelStartsNeverExceedTheCap()
    {
        const int starts = 25;

        HttpResponseMessage[] responses = await Task.WhenAll(
            Enumerable.Range(0, starts).Select(index => Client.StartUploadAsync(ItemsApi.Root, $"file{index}.bin", 1)));

        Assert.Equal(StoragePolicy.MaximumUploadsInProgress, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(starts - StoragePolicy.MaximumUploadsInProgress, responses.Count(response => response.StatusCode == HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task AbandonedUploadsStopCountingTowardsTheCap()
    {
        for (int index = 0; index < StoragePolicy.MaximumUploadsInProgress; index++)
        {
            await Client.StartUploadSessionAsync(ItemsApi.Root, $"file{index}.bin", 1);
        }

        HttpResponseMessage refused = await Client.StartUploadAsync(ItemsApi.Root, "extra.bin", 1);
        Factory.Time.Advance(TimeSpan.FromMinutes(31));
        HttpResponseMessage accepted = await Client.StartUploadAsync(ItemsApi.Root, "extra.bin", 1);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task StartingAnUploadIntoAnotherUsersFolderGives404()
    {
        ItemEntry folder = await Client.CreateFolderEntryAsync(ItemsApi.Root, "private");
        HttpClient bob = await SignInAsBobAsync();

        HttpResponseMessage response = await bob.StartUploadAsync(folder.Id, "a.bin", 1);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task ReserveAsync(string ownerId, long bytes, Func<Task> work)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<StorageReservations>().ReserveAsync(
            ownerId,
            bytes,
            async () =>
            {
                await work();
                return true;
            },
            CancellationToken);
    }

    private Task SetMaxFileSizeAsync(int maxFileSizeMb)
    {
        return SetStorageLimitsAsync(maxFileSizeMb, StoragePolicy.DefaultMinFreeSpaceMb);
    }

    private async Task SetStorageLimitsAsync(int? maxFileSizeMb, int minFreeSpaceMb)
    {
        HttpResponseMessage response = await Client.PutAsJsonAsync(
            "/api/settings/server",
            new ServerSettings(null, MaxFileSizeMb: maxFileSizeMb, MinFreeSpaceMb: minFreeSpaceMb),
            HttpClientExtensions.JsonOptions,
            CancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
