namespace Guardou.Api.Hosting;

internal static class SecurityHeaders
{
    private const string HtmlContentType = "text/html";
    private const string JsonContentType = "json";
    private const string NoSniff = "nosniff";
    private const string ReferrerPolicy = "same-origin";
    private const string PermissionsPolicy = "camera=(), microphone=(), geolocation=()";
    private const string DenyFraming = "DENY";
    private const string SameOriginFraming = "SAMEORIGIN";
    private const string ViewSuffix = "/view";

    private const string HtmlContentSecurityPolicy =
        "default-src 'self'; "
        + "script-src 'self' 'wasm-unsafe-eval'; "
        + "style-src 'self' 'unsafe-inline'; "
        + "img-src 'self' data: blob:; "
        + "media-src 'self' blob:; "
        + "font-src 'self' https://fonts.gstatic.com; "
        + "connect-src 'self' https://fonts.gstatic.com; "
        + "frame-src 'self'; "
        + "object-src 'none'; "
        + "base-uri 'self'; "
        + "form-action 'self'; "
        + "frame-ancestors 'none'";

    private const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use((context, next) =>
        {
            context.Response.OnStarting(AddHeaders, context);
            return next(context);
        });
    }

    private static Task AddHeaders(object state)
    {
        HttpContext context = (HttpContext)state;
        IHeaderDictionary headers = context.Response.Headers;
        headers.TryAdd("X-Content-Type-Options", NoSniff);
        headers.TryAdd("Referrer-Policy", ReferrerPolicy);
        headers.TryAdd("Permissions-Policy", PermissionsPolicy);
        if (context.Request.Path.StartsWithSegments("/api") && context.Request.Path.Value!.EndsWith(ViewSuffix, StringComparison.OrdinalIgnoreCase))
        {
            // The viewer embeds /view in an iframe. Its own Content-Security-Policy (sandbox) is left alone.
            headers.TryAdd("X-Frame-Options", SameOriginFraming);
        }
        else if (context.Response.ContentType?.StartsWith(HtmlContentType, StringComparison.OrdinalIgnoreCase) == true)
        {
            headers.TryAdd("Content-Security-Policy", HtmlContentSecurityPolicy);
            headers.TryAdd("X-Frame-Options", DenyFraming);
        }
        else if (context.Request.Path.StartsWithSegments("/api"))
        {
            if (context.Response.ContentType?.Contains(JsonContentType, StringComparison.OrdinalIgnoreCase) == true)
            {
                headers.TryAdd("Content-Security-Policy", ApiContentSecurityPolicy);
            }

            headers.TryAdd("X-Frame-Options", DenyFraming);
        }

        return Task.CompletedTask;
    }
}
