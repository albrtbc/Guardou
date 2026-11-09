namespace Guardou.Application.Files;

internal sealed record ArchiveLimits(int MaximumEntries, long MaximumBytes)
{
    public static ArchiveLimits Default { get; } = new(10_000, 50L * 1024 * 1024 * 1024);
}
