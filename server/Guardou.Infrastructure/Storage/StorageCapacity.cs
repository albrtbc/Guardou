using Guardou.Application.Quotas;

namespace Guardou.Infrastructure.Storage;

internal sealed class StorageCapacity(StoragePaths paths) : IStorageCapacity
{
    public long AvailableBytes()
    {
        return new DriveInfo(ExistingAncestor(paths.FilesDirectory)).AvailableFreeSpace;
    }

    internal static string ExistingAncestor(string path)
    {
        string current = path;
        while (!Directory.Exists(current) && Path.GetDirectoryName(current) is { Length: > 0 } parent)
        {
            current = parent;
        }

        return current;
    }
}
