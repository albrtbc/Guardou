using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Primitives;

namespace Guardou.IntegrationTests;

// The test server has no client address, so a request header supplies the connection's remote address.
internal sealed class RemoteAddressFilter : IStartupFilter
{
    public const string Header = "X-Test-Remote";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue(Header, out StringValues remote))
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(remote.ToString());
                }

                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
