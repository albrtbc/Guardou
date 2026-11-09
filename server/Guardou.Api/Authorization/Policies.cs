using Guardou.Domain.Users;
using Microsoft.AspNetCore.Authorization;

namespace Guardou.Api.Authorization;

internal static class Policies
{
    public const string Admin = "Admin";

    public static void Add(AuthorizationBuilder authorization)
    {
        authorization.AddPolicy(Admin, policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Admin));
        authorization.SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
    }
}
