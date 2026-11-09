using Guardou.Application.Audit;

namespace Guardou.Application.Users;

public sealed class Registration(IUserAccountStore store, UserAdministration administration, AuditLog audit)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<bool> IsOpenAsync()
    {
        return !await store.AnyUserAsync();
    }

    public async Task<RegistrationResult> RegisterFirstAdministratorAsync(string username, string displayName, string email, string password)
    {
        await Gate.WaitAsync();
        try
        {
            if (!await IsOpenAsync())
            {
                return RegistrationResult.Closed;
            }

            UserAccountResult result = await administration.SetWithoutAuditAsync(username, displayName, email, password, administrator: true);
            if (!result.Succeeded)
            {
                return RegistrationResult.Rejected(result.Errors.Select(error => error.Description).ToList());
            }

            if (await store.FindByUsernameAsync(username) is { } account)
            {
                audit.Registered(account.Id, account.Username);
            }

            return RegistrationResult.Registered;
        }
        finally
        {
            Gate.Release();
        }
    }
}
