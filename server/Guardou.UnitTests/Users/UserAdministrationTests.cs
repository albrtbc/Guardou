using Guardou.Application.Users;
using Guardou.UnitTests.Audit;
using Guardou.UnitTests.Files;
using Microsoft.Extensions.Logging;

namespace Guardou.UnitTests.Users;

public sealed class UserAdministrationTests
{
    private const string Cli = "cli";

    private readonly FakeUserAccountStore _store = new();
    private readonly TestAudit _audit = new(Cli, clientIp: null);

    private UserAdministration Administration()
    {
        return new UserAdministration(_store, _audit.Log);
    }

    [Fact]
    public async Task SetCreatesAMissingUser()
    {
        UserAccountResult result = await Administration()
            .SetAsync("bob", "Bob Jones", "bob@example.com", "password", administrator: false);

        UserAccount bob = _store.ByUsername("bob");
        Assert.True(result.Succeeded);
        Assert.Equal("password", _store.Passwords[bob.Id]);
        Assert.Equal("Bob Jones", bob.DisplayName);
        Assert.Equal("bob@example.com", bob.Email);
        Assert.False(bob.IsAdmin);
    }

    [Fact]
    public async Task SetRequiresAnEmailToCreateAUser()
    {
        UserAccountResult result = await Administration().SetAsync("bob", null, null, "password", administrator: false);

        Assert.Equal(UserAccountErrorCodes.EmailRequired, Assert.Single(result.Errors).Code);
        Assert.Empty(_store.Accounts);
    }

    [Fact]
    public async Task SetChangesThePasswordOfAnExistingUser()
    {
        await _store.CreateAsync(new UserProfile("bob", "Bob", "bob@example.com", IsAdmin: false), "old");

        await Administration().SetAsync("bob", null, null, "new", administrator: false);

        UserAccount bob = _store.ByUsername("bob");
        Assert.Equal("new", _store.Passwords[bob.Id]);
        Assert.Equal("bob@example.com", bob.Email);
    }

    [Fact]
    public async Task SetChangesTheEmailOfAnExistingUserWhenGiven()
    {
        await _store.CreateAsync(new UserProfile("bob", "Bob", "bob@example.com", IsAdmin: false), "old");

        await Administration().SetAsync("bob", null, "robert@example.com", "new", administrator: false);

        Assert.Equal("robert@example.com", _store.ByUsername("bob").Email);
    }

    [Fact]
    public async Task SetUsesTheUsernameAsTheDisplayNameOfANewUser()
    {
        await Administration().SetAsync("bob", null, "bob@example.com", "password", administrator: false);

        Assert.Equal("bob", _store.ByUsername("bob").DisplayName);
    }

    [Fact]
    public async Task SetChangesTheDisplayNameOfAnExistingUserOnlyWhenGiven()
    {
        await _store.CreateAsync(new UserProfile("bob", "Bob", "bob@example.com", IsAdmin: false), "old");
        UserAdministration administration = Administration();

        await administration.SetAsync("bob", null, null, "new", administrator: false);
        string kept = _store.ByUsername("bob").DisplayName;
        await administration.SetAsync("bob", "Robert", null, "new", administrator: false);

        Assert.Equal("Bob", kept);
        Assert.Equal("Robert", _store.ByUsername("bob").DisplayName);
    }

    [Fact]
    public async Task SetKeepsTheAdminRoleOfAnExistingAdmin()
    {
        await _store.CreateAsync(new UserProfile("bob", "Bob", "bob@example.com", IsAdmin: true), "old");

        await Administration().SetAsync("bob", null, null, "new", administrator: false);

        Assert.True(_store.ByUsername("bob").IsAdmin);
    }

    [Fact]
    public async Task SetPromotesWhenAdministratorIsRequested()
    {
        await Administration().SetAsync("bob", null, "bob@example.com", "password", administrator: true);

        Assert.True(_store.ByUsername("bob").IsAdmin);
    }

    [Fact]
    public async Task PromoteGivesTheAdminRole()
    {
        await _store.CreateAsync(new UserProfile("bob", "Bob", "bob@example.com", IsAdmin: false), "password");

        UserAccountResult result = await Administration().PromoteAsync("bob");

        Assert.True(result.Succeeded);
        Assert.True(_store.ByUsername("bob").IsAdmin);
        Assert.Equal("password", _store.Passwords[_store.ByUsername("bob").Id]);
    }

    [Fact]
    public async Task PromoteFailsForAMissingUser()
    {
        UserAccountResult result = await Administration().PromoteAsync("ghost");

        Assert.Equal(UserAccountErrorCodes.UserNotFound, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task LogsTheCreationOfAUserAsTheCli()
    {
        await Administration().SetAsync("bob", "Bob Jones", "bob@example.com", "Sup3r-secret-pass", administrator: true);

        LogRecord record = Assert.Single(_audit.Logger.Records);
        Assert.Equal(9010, record.EventId.Id);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(Cli, record.Properties["ActorId"]);
        Assert.Null(record.Properties["ClientIp"]);
        Assert.Equal(_store.ByUsername("bob").Id, record.Properties["UserId"]);
        Assert.Equal(true, record.Properties["IsAdmin"]);
        Assert.DoesNotContain("Sup3r-secret-pass", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LogsNothingWhenSetFails()
    {
        await Administration().SetAsync("bob", null, null, "password", administrator: false);

        Assert.Empty(_audit.Logger.Records);
    }

    [Fact]
    public async Task LogsAnUpdateAsTheCliWithoutARoleChangeWhenTheRoleIsKept()
    {
        await _store.CreateAsync(new UserProfile("bob", "Bob", "bob@example.com", IsAdmin: true), "old");

        await Administration().SetAsync("bob", null, null, "new", administrator: true);

        LogRecord record = Assert.Single(_audit.Logger.Records);
        Assert.Equal(9011, record.EventId.Id);
        Assert.Equal(Cli, record.Properties["ActorId"]);
        Assert.Equal(true, record.Properties["PasswordChanged"]);
    }

    [Fact]
    public async Task LogsAnUpdateAndTheRoleChangeWhenSetGrantsTheAdminRole()
    {
        await _store.CreateAsync(new UserProfile("bob", "Bob", "bob@example.com", IsAdmin: false), "old");

        await Administration().SetAsync("bob", null, null, "new", administrator: true);

        Assert.Equal([9011, 9013], _audit.Logger.Records.Select(record => record.EventId.Id));
        Assert.All(_audit.Logger.Records, record => Assert.Equal(Cli, record.Properties["ActorId"]));
    }

    [Fact]
    public async Task LogsThePromotionAsTheCliOnlyWhenTheUserWasNotAnAdministrator()
    {
        await _store.CreateAsync(new UserProfile("bob", "Bob", "bob@example.com", IsAdmin: false), "password");

        await Administration().PromoteAsync("bob");
        await Administration().PromoteAsync("bob");
        await Administration().PromoteAsync("ghost");

        LogRecord record = Assert.Single(_audit.Logger.Records);
        Assert.Equal(9013, record.EventId.Id);
        Assert.Equal(Cli, record.Properties["ActorId"]);
        Assert.Equal(_store.ByUsername("bob").Id, record.Properties["UserId"]);
        Assert.Equal(true, record.Properties["IsAdmin"]);
    }
}
