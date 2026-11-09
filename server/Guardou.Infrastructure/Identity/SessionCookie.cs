using System.Globalization;
using Guardou.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Guardou.Infrastructure.Identity;

internal static class SessionCookie
{
    private const string AuthTimeKey = "auth_time";

    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    public static void Configure(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = Product.CookieName("session");
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = Lifetime;
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context => WriteStatusCode(context, StatusCodes.Status401Unauthorized);
        options.Events.OnRedirectToAccessDenied = context => WriteStatusCode(context, StatusCodes.Status403Forbidden);
        Func<CookieValidatePrincipalContext, Task> validate = options.Events.OnValidatePrincipal;
        options.Events.OnValidatePrincipal = context => ValidateWithoutRenewalAsync(validate, context);
    }

    public static void StampAuthTime(AuthenticationProperties properties, DateTimeOffset now)
    {
        properties.Items[AuthTimeKey] = now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
    }

    // A session without a readable auth_time (issued before the cap existed) is refused, so the cap cannot be skipped.
    public static bool HasExceededMaxAge(AuthenticationProperties? properties, DateTimeOffset now)
    {
        return properties is null
            || !properties.Items.TryGetValue(AuthTimeKey, out string? value)
            || !long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long seconds)
            || now - DateTimeOffset.FromUnixTimeSeconds(seconds) > MaxAge;
    }

    private static async Task ValidateWithoutRenewalAsync(
        Func<CookieValidatePrincipalContext, Task> validate,
        CookieValidatePrincipalContext context)
    {
        TimeProvider time = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
        if (HasExceededMaxAge(context.Properties, time.GetUtcNow()))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(context.Scheme.Name);
            return;
        }

        await validate(context);
        context.ShouldRenew = false;
    }

    private static Task WriteStatusCode(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
    {
        context.Response.StatusCode = statusCode;
        return Task.CompletedTask;
    }
}
