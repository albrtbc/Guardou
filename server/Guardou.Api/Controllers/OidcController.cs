using System.Text.Json;
using Guardou.Api.Hosting;
using Guardou.Application.Authentication;
using Guardou.Domain.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Guardou.Api.Controllers;

[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/auth/oidc")]
public sealed class OidcController(ExternalSignIn externalSignIn) : ControllerBase
{
    private const string NotConfiguredCode = "notConfigured";
    private const string HomePath = "/";

    [EnableRateLimiting(RateLimits.Auth)]
    [HttpGet("login")]
    public async Task<IActionResult> Login(string? returnUrl, CancellationToken cancellationToken)
    {
        if (!await externalSignIn.IsAvailableAsync(cancellationToken))
        {
            return LocalRedirect(OidcEndpoints.LoginError(NotConfiguredCode));
        }

        string? completion = Url.Action(nameof(Complete), new { returnUrl = LocalOrNull(returnUrl) });
        return Challenge(new AuthenticationProperties { RedirectUri = completion }, OidcSettings.Provider);
    }

    [EnableRateLimiting(RateLimits.Auth)]
    [HttpGet("complete")]
    public async Task<IActionResult> Complete(string? returnUrl, CancellationToken cancellationToken)
    {
        ExternalSignInStatus status = await externalSignIn.SignInAsync(cancellationToken);
        return LocalRedirect(status == ExternalSignInStatus.SignedIn
            ? LocalOrNull(returnUrl) ?? HomePath
            : OidcEndpoints.LoginError(JsonNamingPolicy.CamelCase.ConvertName(status.ToString())));
    }

    private string? LocalOrNull(string? url)
    {
        return Url.IsLocalUrl(url) ? url : null;
    }
}
