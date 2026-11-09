namespace Guardou.Domain.Files;

public sealed class Upload
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private static readonly TimeSpan ActiveWindow = TimeSpan.FromMinutes(30);

    private Upload(
        string id,
        string ownerId,
        Guid folderId,
        string folderPath,
        string name,
        long size,
        DateTimeOffset? modified,
        DateTimeOffset lastWrite)
    {
        Id = id;
        OwnerId = ownerId;
        FolderId = folderId;
        FolderPath = folderPath;
        Name = name;
        Size = size;
        Modified = modified;
        LastWrite = lastWrite;
    }

    public string Id { get; private set; }

    public string OwnerId { get; private set; }

    public Guid FolderId { get; private set; }

    public string FolderPath { get; private set; }

    public string Name { get; private set; }

    public long Size { get; private set; }

    public DateTimeOffset? Modified { get; private set; }

    public DateTimeOffset LastWrite { get; private set; }

    public UploadDestination Destination => new(FolderId, FolderPath, Name);

    public static Upload Create(string id, string ownerId, UploadDestination destination, long size, DateTimeOffset? modified, DateTimeOffset now)
    {
        return new Upload(id, ownerId, destination.FolderId, destination.FolderPath, destination.Name, size, modified, now);
    }

    public static DateTimeOffset ActiveSince(DateTimeOffset now)
    {
        return now - ActiveWindow;
    }

    public bool IsActive(DateTimeOffset now)
    {
        return LastWrite > ActiveSince(now);
    }

    public void RecordWrite(DateTimeOffset now)
    {
        LastWrite = now;
    }
}
