using Guardou.Application.Settings;
using Guardou.Application.Uploads;
using Guardou.Domain.Files;
using Guardou.Domain.Settings;

namespace Guardou.Application.Quotas;

internal sealed class StorageReservations(
    IStorageQuotaStore quotas,
    StorageQuotaLocks locks,
    StorageWrites writes,
    IStorageCapacity capacity,
    IUploadStore uploads,
    IUploadContent content,
    EffectiveSettings<ServerSettings> settings)
{
    public async Task<T> ReserveAsync<T>(string ownerId, long bytes, Func<Task<T>> work, CancellationToken cancellationToken)
    {
        SemaphoreSlim ownerLock = locks.For(ownerId);
        await ownerLock.WaitAsync(cancellationToken);
        try
        {
            if (!(await quotas.FindAsync(ownerId, cancellationToken)).Permits(bytes))
            {
                throw new QuotaExceededException();
            }

            await TrackWriteAsync(bytes, cancellationToken);
            try
            {
                return await work();
            }
            finally
            {
                writes.Remove(bytes);
            }
        }
        finally
        {
            ownerLock.Release();
        }
    }

    public async Task<bool> HasRoomToWriteAsync(long bytes, CancellationToken cancellationToken)
    {
        return capacity.AvailableBytes() - await ReserveBytesAsync(cancellationToken) >= bytes;
    }

    private async Task TrackWriteAsync(long bytes, CancellationToken cancellationToken)
    {
        await writes.Gate.WaitAsync(cancellationToken);
        try
        {
            long taken = await PendingUploadBytesAsync(cancellationToken) + writes.InFlight;
            if (capacity.AvailableBytes() - await ReserveBytesAsync(cancellationToken) - taken < bytes)
            {
                throw new InsufficientStorageException();
            }

            writes.Add(bytes);
        }
        finally
        {
            writes.Gate.Release();
        }
    }

    private async Task<long> PendingUploadBytesAsync(CancellationToken cancellationToken)
    {
        long pending = 0;
        foreach (UploadSize upload in await uploads.SizesAsync(cancellationToken))
        {
            pending += Math.Max(0, upload.Size - await content.WrittenBytesAsync(upload.Id, cancellationToken));
        }

        return pending;
    }

    private async Task<long> ReserveBytesAsync(CancellationToken cancellationToken)
    {
        return StoragePolicy.BytesOf((await settings.GetAsync(cancellationToken)).MinFreeSpaceMb);
    }
}
