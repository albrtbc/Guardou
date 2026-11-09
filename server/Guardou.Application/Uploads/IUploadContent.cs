namespace Guardou.Application.Uploads;

public interface IUploadContent
{
    Task<string> CreateAsync(long size, CancellationToken cancellationToken);

    Task<long> WrittenBytesAsync(string id, CancellationToken cancellationToken);

    Task<bool> IsCompleteAsync(string id, CancellationToken cancellationToken);

    Task MoveToBlobAsync(string id, string storageKey, DateTimeOffset modified, CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);

    Task RemoveExpiredAsync(CancellationToken cancellationToken);
}
