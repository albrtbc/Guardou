using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Guardou.Api.Hosting;

internal static class RateLimits
{
    public const string Auth = "auth";
    public const string Shares = "shares";
    public const string SharePreviews = "share-previews";

    private const string UnknownClient = "unknown";
    private const int Ipv6PrefixBytes = 8;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static void Add(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = AddRetryAfter;
            options.AddPolicy(Auth, context => RateLimitPartition.GetFixedWindowLimiter(ClientOf(context), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = Window,
                QueueLimit = 0,
            }));
            options.AddPolicy(Shares, context => RateLimitPartition.GetTokenBucketLimiter(ClientOf(context), _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 120,
                TokensPerPeriod = 60,
                ReplenishmentPeriod = Window,
                QueueLimit = 0,
            }));
            options.AddPolicy(SharePreviews, context => RateLimitPartition.GetTokenBucketLimiter(ClientOf(context), _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 600,
                TokensPerPeriod = 300,
                ReplenishmentPeriod = Window,
                QueueLimit = 0,
            }));
        });
    }

    private static string ClientOf(HttpContext context)
    {
        IPAddress? address = context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return UnknownClient;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        // One IPv6 subscriber holds a whole /64, so a per-address key would give an attacker endless budgets.
        Span<byte> prefix = stackalloc byte[16];
        address.TryWriteBytes(prefix, out _);
        prefix[Ipv6PrefixBytes..].Clear();
        return $"{new IPAddress(prefix)}/64";
    }

    private static ValueTask AddRetryAfter(OnRejectedContext context, CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        return ValueTask.CompletedTask;
    }
}
