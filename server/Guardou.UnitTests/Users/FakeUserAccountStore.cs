using Guardou.Application.Users;

namespace Guardou.UnitTests.Users;

internal sealed class FakeUserAccountStore : IUserAccountStore
{
    public Dictionary<string, UserAccount> Accounts { get; } = [];

    public Dictionary<string, string> Passwords { get; } = [];

    public Dictionary<(string Provider, string Key), string> Logins { get; } = [];

    public int Updates { get; private set; }

    public Task<bool> AnyUserAsync()
    {
        return Task.FromResult(Accounts.Count > 0);
    }

    public Task<IReadOnlyList<UserAccount>> ListAsync()
    {
        return Task.FromResult<IReadOnlyList<UserAccount>>(Accounts.Values.Select(WithLogins).ToList());
    }

    public Task<UserAccount?> FindAsync(string id)
    {
        return Find(account => account.Id == id);
    }

    public Task<UserAccount?> FindByUsernameAsync(string username)
    {
        return Find(account => account.Username == username);
    }

    public Task<UserAccount?> FindByEmailAsync(string email)
    {
        return Find(account => string.Equals(account.Email, email, StringComparison.OrdinalIgnoreCase));
    }

    public Task<UserAccount?> FindByLoginAsync(string provider, string key)
    {
        return Logins.TryGetValue((provider, key), out string? id) ? FindAsync(id) : Task.FromResult<UserAccount?>(null);
    }

    public Task<UserAccountResult> CreateAsync(UserProfile profile, string password)
    {
        UserAccountResult result = Add(profile);
        if (result.Succeeded)
        {
            Passwords.Add(IdOf(profile.Username), password);
        }

        return Task.FromResult(result);
    }

    public Task<UserAccountResult> CreateExternalAsync(UserProfile profile, string provider, string key)
    {
        UserAccountResult result = Add(profile);
        if (result.Succeeded)
        {
            Logins.Add((provider, key), IdOf(profile.Username));
        }

        return Task.FromResult(result);
    }

    public Task<UserAccountResult> UpdateAsync(string id, UserProfile profile, string? password)
    {
        Updates++;
        Accounts[id] = AccountOf(id, profile);
        if (password is not null)
        {
            Passwords[id] = password;
        }

        return Task.FromResult(UserAccountResult.Success);
    }

    public Task<UserAccountResult> AddLoginAsync(string id, string provider, string key)
    {
        Logins.Add((provider, key), id);
        return Task.FromResult(UserAccountResult.Success);
    }

    public Task<bool> RemoveLoginAsync(string id, string provider)
    {
        List<(string Provider, string Key)> removed = Logins.Where(login => login.Value == id && login.Key.Provider == provider).Select(login => login.Key).ToList();
        foreach ((string Provider, string Key) login in removed)
        {
            Logins.Remove(login);
        }

        return Task.FromResult(removed.Count > 0);
    }

    public Task SetQuotaAsync(string id, long? quotaBytes)
    {
        Accounts[id] = Accounts[id] with { QuotaBytes = quotaBytes };
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id)
    {
        Accounts.Remove(id);
        return Task.CompletedTask;
    }

    public UserAccount ByUsername(string username)
    {
        return WithLogins(Accounts.Values.Single(account => account.Username == username));
    }

    private static string IdOf(string username)
    {
        return $"id-{username}";
    }

    private static UserAccount AccountOf(string id, UserProfile profile)
    {
        return new UserAccount(id, profile.Username, profile.DisplayName, profile.Email, profile.IsAdmin, HasPicture: false, [], QuotaBytes: null);
    }

    private UserAccountResult Add(UserProfile profile)
    {
        if (Accounts.Values.Any(account => string.Equals(account.Email, profile.Email, StringComparison.OrdinalIgnoreCase)))
        {
            return UserAccountResult.Failure("DuplicateEmail", $"Email '{profile.Email}' is already taken.");
        }

        string id = IdOf(profile.Username);
        Accounts.Add(id, AccountOf(id, profile));
        return UserAccountResult.Success;
    }

    private Task<UserAccount?> Find(Func<UserAccount, bool> predicate)
    {
        UserAccount? account = Accounts.Values.SingleOrDefault(predicate);
        return Task.FromResult(account is null ? null : WithLogins(account));
    }

    private UserAccount WithLogins(UserAccount account)
    {
        List<string> providers = Logins.Where(login => login.Value == account.Id).Select(login => login.Key.Provider).Distinct().ToList();
        return account with { ExternalLogins = providers };
    }
}
