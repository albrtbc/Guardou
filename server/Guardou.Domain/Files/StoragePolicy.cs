namespace Guardou.Domain.Files;

public static class StoragePolicy
{
    public const int DefaultMinFreeSpaceMb = 1024;
    public const int MinimumMinFreeSpaceMb = 0;
    public const int MinimumMaxFileSizeMb = 1;
    public const int MaximumMb = 1_048_576;
    public const int MaximumUploadsInProgress = 20;

    private const long BytesPerMegabyte = 1024 * 1024;

    public static bool IsValidMaxFileSize(int? maxFileSizeMb)
    {
        return maxFileSizeMb is null or (>= MinimumMaxFileSizeMb and <= MaximumMb);
    }

    public static bool IsValidMinFreeSpace(int minFreeSpaceMb)
    {
        return minFreeSpaceMb is >= MinimumMinFreeSpaceMb and <= MaximumMb;
    }

    public static long BytesOf(int megabytes)
    {
        return megabytes * BytesPerMegabyte;
    }

    public static long? BytesOf(int? megabytes)
    {
        return megabytes is { } value ? BytesOf(value) : null;
    }
}
