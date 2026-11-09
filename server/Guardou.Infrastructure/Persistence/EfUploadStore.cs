using Guardou.Application.Uploads;
using Guardou.Domain.Files;
using Microsoft.EntityFrameworkCore;

namespace Guardou.Infrastructure.Persistence;

internal sealed class EfUploadStore(AppDbContext context) : IUploadStore
{
    private DbSet<Upload> Uploads => context.Set<Upload>();

    public Task<Upload?> FindAsync(string id, CancellationToken cancellationToken)
    {
        return Uploads.SingleOrDefaultAsync(upload => upload.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Upload>> ToDestinationAsync(UploadDestination destination, CancellationToken cancellationToken)
    {
        return await Uploads
            .Where(upload => upload.FolderId == destination.FolderId
                && upload.FolderPath == destination.FolderPath
                && upload.Name == destination.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountActiveOfOwnerAsync(string ownerId, DateTimeOffset since, CancellationToken cancellationToken)
    {
        return Uploads.CountAsync(upload => upload.OwnerId == ownerId && upload.LastWrite > since, cancellationToken);
    }

    public async Task<IReadOnlyList<UploadSize>> SizesAsync(CancellationToken cancellationToken)
    {
        return await Uploads.Select(upload => new UploadSize(upload.Id, upload.Size)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Upload>> LastWrittenBeforeAsync(DateTimeOffset time, CancellationToken cancellationToken)
    {
        return await Uploads.Where(upload => upload.LastWrite <= time).ToListAsync(cancellationToken);
    }

    public void Add(Upload upload)
    {
        Uploads.Add(upload);
    }

    public void Remove(Upload upload)
    {
        Uploads.Remove(upload);
    }

    public Task SaveAsync(CancellationToken cancellationToken)
    {
        return context.SaveChangesAsync(cancellationToken);
    }
}
