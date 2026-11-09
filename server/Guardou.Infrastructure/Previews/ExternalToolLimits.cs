namespace Guardou.Infrastructure.Previews;

internal sealed record ExternalToolLimits(TimeSpan Timeout, long MaximumOutputBytes, int MaximumErrorBytes)
{
    public static ExternalToolLimits Default { get; } = new(TimeSpan.FromMinutes(2), 64L * 1024 * 1024, 64 * 1024);
}
