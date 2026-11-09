using System.Security.Claims;
using Guardou.Application.Audit;
using Microsoft.AspNetCore.Http;

namespace Guardou.Infrastructure.Web;

// The client address is the connection's, which the forwarded headers middleware has already replaced
// with the address a trusted proxy reported.
internal sealed class HttpAuditContext(IHttpContextAccessor accessor) : IAuditContext
{
    public string? ActorId => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? ClientIp
    {
        get
        {
            System.Net.IPAddress? address = accessor.HttpContext?.Connection.RemoteIpAddress;
            return (address is { IsIPv4MappedToIPv6: true } ? address.MapToIPv4() : address)?.ToString();
        }
    }
}
