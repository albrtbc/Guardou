namespace Guardou.Application.Files;

public sealed class ArchiveTooLargeException : Exception
{
    public const string Code = "archiveTooLarge";

    internal ArchiveTooLargeException(ArchiveLimits limits)
        : base($"The selection is too large for one archive: the limit is {limits.MaximumEntries} entries and {limits.MaximumBytes / (1024 * 1024 * 1024)} GB.")
    {
    }
}
