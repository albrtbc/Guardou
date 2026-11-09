using Guardou.Application.Uploads;
using Guardou.Domain.Files;
using Guardou.Infrastructure.Files;
using Guardou.Infrastructure.Storage;
using tusdotnet.Stores;

namespace Guardou.Infrastructure.Uploads;

internal sealed class TusUploadContent(TusDiskStore store, StoragePaths paths, LocalBlobStore blobs, TimeProvider time) : IUploadContent
{
    public async Task<string> CreateAsync(long size, CancellationToken cancellationToken)
    {
        string id = await store.CreateFileAsync(size, string.Empty, cancellationToken);
        await store.SetExpirationAsync(id, time.GetUtcNow() + Upload.Lifetime, cancellationToken);
        return id;
    }

    public async Task<long> WrittenBytesAsync(string id, CancellationToken cancellationToken)
    {
        // Completing, cancelling or expiring an upload can remove its file between the check and the read.
        try
        {
            return await store.FileExistAsync(id, cancellationToken) ? await store.GetUploadOffsetAsync(id, cancellationToken) : 0;
        }
        catch (FileNotFoundException)
        {
            return 0;
        }
    }

    public async Task<bool> IsCompleteAsync(string id, CancellationToken cancellationToken)
    {
        return await store.FileExistAsync(id, cancellationToken)
            && await store.GetUploadLengthAsync(id, cancellationToken) == await store.GetUploadOffsetAsync(id, cancellationToken);
    }

    public Task MoveToBlobAsync(string id, string storageKey, DateTimeOffset modified, CancellationToken cancellationToken)
    {
        string target = blobs.PathOf(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(target) ?? paths.FilesDirectory);
        File.Move(ContentPath(id), target);
        File.SetLastWriteTimeUtc(target, modified.UtcDateTime);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        return store.DeleteFileAsync(id, cancellationToken);
    }

    public Task RemoveExpiredAsync(CancellationToken cancellationToken)
    {
        return store.RemoveExpiredFilesAsync(cancellationToken);
    }

    private string ContentPath(string id)
    {
        return SafePathResolver.Resolve(paths.UploadsDirectory, id)
            ?? throw new InvalidOperationException($"The upload id '{id}' leaves the uploads directory.");
    }
}
