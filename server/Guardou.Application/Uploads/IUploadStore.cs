using Guardou.Domain.Files;

namespace Guardou.Application.Uploads;

public interface IUploadStore
{
    Task<Upload?> FindAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Upload>> ToDestinationAsync(UploadDestination destination, CancellationToken cancellationToken);

    Task<int> CountActiveOfOwnerAsync(string ownerId, DateTimeOffset since, CancellationToken cancellationToken);

    Task<IReadOnlyList<UploadSize>> SizesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Upload>> LastWrittenBeforeAsync(DateTimeOffset time, CancellationToken cancellationToken);

    void Add(Upload upload);

    void Remove(Upload upload);

    Task SaveAsync(CancellationToken cancellationToken);
}
