using Guardou.Application.Audit;

namespace Guardou.Application.Users;

public sealed class UserAdministration(IUserAccountStore store, AuditLog audit)
{
    public async Task<UserAccountResult> SetAsync(
        string username,
        string? displayName,
        string? email,
        string password,
        bool administrator)
    {
        UserAccount? existing = await store.FindByUsernameAsync(username);
        UserAccountResult result = await ApplyAsync(existing, username, displayName, email, password, administrator);
        if (result.Succeeded)
        {
            await AuditSetAsync(existing, username, administrator);
        }

        return result;
    }

    public async Task<UserAccountResult> PromoteAsync(string username)
    {
        UserAccount? account = await store.FindByUsernameAsync(username);
        if (account is null)
        {
            return UserAccountResult.Failure(UserAccountErrorCodes.UserNotFound, $"User '{username}' does not exist.");
        }

        UserAccountResult result = await store.UpdateAsync(account.Id, account.Profile with { IsAdmin = true }, password: null);
        if (result.Succeeded && !account.IsAdmin)
        {
            audit.UserRoleChanged(account.Id, isAdmin: true);
        }

        return result;
    }

    // The registration of the first administrator logs its own event, so it does not go through the audited path.
    internal async Task<UserAccountResult> SetWithoutAuditAsync(
        string username,
        string? displayName,
        string? email,
        string password,
        bool administrator)
    {
        return await ApplyAsync(await store.FindByUsernameAsync(username), username, displayName, email, password, administrator);
    }

    private async Task<UserAccountResult> ApplyAsync(
        UserAccount? account,
        string username,
        string? displayName,
        string? email,
        string password,
        bool administrator)
    {
        if (account is not null)
        {
            UserProfile profile = new(
                username,
                displayName ?? account.DisplayName,
                email ?? account.Email,
                account.IsAdmin || administrator);
            return await store.UpdateAsync(account.Id, profile, password);
        }

        return email is null
            ? UserAccountResult.Failure(UserAccountErrorCodes.EmailRequired, "An email is required to create a user.")
            : await store.CreateAsync(new UserProfile(username, displayName ?? username, email, administrator), password);
    }

    private async Task AuditSetAsync(UserAccount? existing, string username, bool administrator)
    {
        if (existing is not null)
        {
            audit.UserUpdated(existing.Id, passwordChanged: true);
            if (administrator && !existing.IsAdmin)
            {
                audit.UserRoleChanged(existing.Id, isAdmin: true);
            }
        }
        else if (await store.FindByUsernameAsync(username) is { } created)
        {
            audit.UserCreated(created.Id, created.Username, created.IsAdmin);
        }
    }
}
