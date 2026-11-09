using System.Security.Claims;
using Guardou.Application.Audit;
using Guardou.Application.Authentication;
using Guardou.Domain.Authentication;
using Guardou.Infrastructure.Identity.Oidc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Guardou.Infrastructure.Identity;

internal sealed class IdentitySessionService(
    SignInManager<ApplicationUser> signInManager,
    OidcSignOut oidcSignOut,
    TimeProvider time,
    AuditLog audit) : ISessionService
{
    private const string IdTokenName = "id_token";

    private static readonly ApplicationUser Unknown = new() { DisplayName = string.Empty };

    private static string? s_unknownHash;

    private HttpContext Context => signInManager.Context;

    public async Task<bool> SignInAsync(string username, string password, bool persistent)
    {
        UserManager<ApplicationUser> users = signInManager.UserManager;
        if (await users.FindByNameAsync(username) is not { } user)
        {
            SpendPasswordCheck(users, password);
            audit.SignInFailed(username);
            return false;
        }

        if (!user.LockoutEnabled)
        {
            await users.SetLockoutEnabledAsync(user, enabled: true);
        }

        // Identity refuses a locked-out account, or one with no password (created through OIDC), without hashing anything.
        // Spend the check here so neither answers faster than a wrong password, and the response does not reveal them.
        bool lockedOut = await users.IsLockedOutAsync(user);
        if (lockedOut || user.PasswordHash is null)
        {
            SpendPasswordCheck(users, password);
            if (lockedOut)
            {
                audit.SignInRefusedLockedOut(user.Id, username);
            }
            else
            {
                audit.SignInFailed(username);
            }

            return false;
        }

        SignInResult result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                audit.LockoutStarted(user.Id, username);
            }
            else
            {
                audit.SignInFailed(username);
            }

            return false;
        }

        AuthenticationProperties properties = new() { IsPersistent = persistent };
        SessionCookie.StampAuthTime(properties, time.GetUtcNow());
        await signInManager.SignInWithClaimsAsync(user, properties, [new Claim("amr", "pwd")]);
        audit.SignInSucceeded(user.Id, username);
        return true;
    }

    // One PBKDF2 verification against a dummy hash, so a refusal that needs no real check costs the same as a wrong password.
    // The dummy hash uses the configured iteration count. Accounts that have not logged in since the count was raised
    // still hold older, cheaper hashes (100k iterations) until their next successful login, so a wrong password on
    // those costs less than this check.
    private static void SpendPasswordCheck(UserManager<ApplicationUser> users, string password)
    {
        s_unknownHash ??= users.PasswordHasher.HashPassword(Unknown, Guid.NewGuid().ToString("N"));
        users.PasswordHasher.VerifyHashedPassword(Unknown, s_unknownHash, password);
    }

    public async Task<ExternalLogin?> ReadExternalLoginAsync()
    {
        AuthenticateResult result = await Context.AuthenticateAsync(IdentityConstants.ExternalScheme);
        return result.Succeeded
            ? new ExternalLogin(result.Principal.Claims.ToList(), result.Properties.GetTokenValue(IdTokenName))
            : null;
    }

    public async Task SignInExternalAsync(string userId, ExternalLogin login)
    {
        ApplicationUser user = await signInManager.UserManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"The user '{userId}' does not exist.");
        AuthenticationProperties properties = new() { IsPersistent = true };
        SessionCookie.StampAuthTime(properties, time.GetUtcNow());
        if (login.IdToken is { } idToken)
        {
            properties.StoreTokens([new AuthenticationToken { Name = IdTokenName, Value = idToken }]);
        }

        await Context.SignOutAsync(IdentityConstants.ExternalScheme);
        await signInManager.SignInAsync(user, properties, OidcSettings.Provider);
    }

    public async Task<string?> SignOutAsync(CancellationToken cancellationToken)
    {
        audit.SignedOut();
        string? idToken = await Context.GetTokenAsync(IdentityConstants.ApplicationScheme, IdTokenName);
        await signInManager.SignOutAsync();
        return idToken is null ? null : await oidcSignOut.EndSessionUrlAsync(Context.Request, idToken, cancellationToken);
    }

    public async Task RefreshAsync(string userId)
    {
        if (await signInManager.UserManager.FindByIdAsync(userId) is { } user)
        {
            await signInManager.RefreshSignInAsync(user);
        }
    }
}
