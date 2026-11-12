using System.Net;
using System.Net.Sockets;

namespace Guardou.Infrastructure.Profiles;

internal static class PublicAddressConnector
{
    public static SocketsHttpHandler CreateHandler()
    {
        return new SocketsHttpHandler { AllowAutoRedirect = false, ConnectCallback = ConnectAsync };
    }

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        IPAddress[] addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        if (addresses.Length == 0 || !addresses.All(PictureSourceRules.Permits))
        {
            throw new HttpRequestException($"The host '{context.DnsEndPoint.Host}' is not a public address.");
        }

        Socket socket = new(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
