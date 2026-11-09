using System.Net.Mime;
using System.Security.Claims;
using Guardou.Api.Authentication;
using Guardou.Api.Hosting;
using Guardou.Api.Models;
using Guardou.Application.Authentication;
using Guardou.Application.Users;
using Guardou.Domain.Users;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Guardou.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    ISessionService sessions,
    PasswordLogin passwordLogin,
    Registration registration,
    UserAccounts accounts,
    IAntiforgery antiforgery)
    : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Auth)]
    [HttpPost("login", Name = "Login")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        PasswordLoginStatus status = await passwordLogin.SignInAsync(request.Username, request.Password, request.RememberMe, cancellationToken);
        return status switch
        {
            PasswordLoginStatus.Disabled => Problem(statusCode: StatusCodes.Status403Forbidden, title: "The password login is disabled."),
            PasswordLoginStatus.InvalidCredentials => Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid username or password."),
            _ => NoContent(),
        };
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Auth)]
    [HttpPost("register", Name = "Register")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        RegistrationResult result = await registration.RegisterFirstAdministratorAsync(request.Username, request.DisplayName, request.Email, request.Password);
        return result.Status switch
        {
            RegistrationStatus.Closed => Problem(statusCode: StatusCodes.Status403Forbidden, title: "The registration is closed."),
            RegistrationStatus.Rejected => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The registration is not valid.",
                detail: string.Join(" ", result.Errors)),
            _ => await SignInRegisteredUserAsync(request),
        };
    }

    [Authorize]
    [HttpPost("logout", Name = "Logout")]
    [ProducesResponseType<LogoutResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<LogoutResult> Logout(CancellationToken cancellationToken)
    {
        return new LogoutResult(await sessions.SignOutAsync(cancellationToken));
    }

    [Authorize]
    [HttpGet("me", Name = "GetCurrentUser")]
    [ProducesResponseType<CurrentUser>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<CurrentUser> Me()
    {
        UserAccount account = await accounts.GetAsync(User.UserId());
        return new CurrentUser(account.Id, account.Username, account.DisplayName, account.Email, account.IsAdmin, account.HasPicture);
    }

    [AllowAnonymous]
    [HttpGet("antiforgery", Name = "GetAntiforgeryToken")]
    [ProducesResponseType<AntiforgeryToken>(StatusCodes.Status200OK)]
    public ActionResult<AntiforgeryToken> Antiforgery()
    {
        AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return new AntiforgeryToken(tokens.RequestToken ?? throw new InvalidOperationException("The antiforgery request token is missing."));
    }

    private async Task<IActionResult> SignInRegisteredUserAsync(RegisterRequest request)
    {
        await sessions.SignInAsync(request.Username, request.Password, persistent: false);
        return NoContent();
    }
}
