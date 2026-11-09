using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace Guardou.Api.Configuration;

internal static class TrustedProxies
{
    public const string Variable = "TRUSTED_PROXIES";
    public const string Key = "Hosting:TrustedProxies";

    private const int IPv4Parts = 4;

    public static void Apply(ForwardedHeadersOptions options, string? list)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        foreach (string entry in (list ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (entry.Contains('/') && IsWellFormed(entry[..entry.IndexOf('/')]) && System.Net.IPNetwork.TryParse(entry, out System.Net.IPNetwork network))
            {
                options.KnownIPNetworks.Add(network);
            }
            else if (!entry.Contains('/') && IsWellFormed(entry) && IPAddress.TryParse(entry, out IPAddress? address))
            {
                options.KnownProxies.Add(address);
            }
            else
            {
                throw new InvalidOperationException(
                    $"{Variable} contains '{entry}', which is not an IP address or a CIDR range. IPv4 addresses need all four parts, like 172.18.0.2.");
            }
        }
    }

    // IPAddress.TryParse accepts shorthand such as "172.18" (172.0.0.18), so a typo would trust the wrong host.
    private static bool IsWellFormed(string address)
    {
        return address.Contains(':') || address.Split('.').Length == IPv4Parts;
    }
}
