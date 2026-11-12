using System.Net;
using System.Net.Sockets;

namespace Guardou.Infrastructure.Profiles;

internal static class PictureSourceRules
{
    public static bool Permits(Uri source)
    {
        return source.IsAbsoluteUri && source.Scheme == Uri.UriSchemeHttps;
    }

    public static bool Permits(IPAddress address)
    {
        IPAddress normalized = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return normalized.AddressFamily == AddressFamily.InterNetwork
            ? PermitsIPv4(normalized.GetAddressBytes())
            : PermitsIPv6(normalized);
    }

    private static bool PermitsIPv4(byte[] bytes)
    {
        return bytes[0] switch
        {
            0 or 10 or 127 => false,
            169 => bytes[1] != 254,
            172 => bytes[1] is < 16 or > 31,
            192 => bytes[1] != 168,
            >= 224 => false,
            _ => true,
        };
    }

    private static bool PermitsIPv6(IPAddress address)
    {
        byte first = address.GetAddressBytes()[0];
        return !IPAddress.IsLoopback(address)
            && !address.Equals(IPAddress.IPv6None)
            && !address.IsIPv6LinkLocal
            && !address.IsIPv6SiteLocal
            && !address.IsIPv6Multicast
            && (first & 0xFE) != 0xFC;
    }
}
