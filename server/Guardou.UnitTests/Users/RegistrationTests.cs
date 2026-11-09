using Guardou.Application.Users;
using Guardou.UnitTests.Audit;
using Guardou.UnitTests.Files;

namespace Guardou.UnitTests.Users;

public sealed class RegistrationTests
{
    private readonly FakeUserAccountStore _store = new();
    private readonly TestAudit _audit = new();

    private Registration CreateRegistration()
    {
        return new Registration(_store, new UserAdministration(_store, _audit.Log), _audit.Log);
    }

    [Fact]
    public async Task IsOpenWhenNoUserExists()
    {
        Assert.True(await CreateRegistration().IsOpenAsync());
    }

    [Fact]
    public async Task TheFirstUserBecomesAnAdministrator()
    {
        RegistrationResult result = await CreateRegistration().RegisterFirstAdministratorAsync("alice", "Alice", "alice@example.com", "password");

        Assert.Equal(RegistrationStatus.Registered, result.Status);
        Assert.True(_store.ByUsername("alice").IsAdmin);
    }

    [Fact]
    public async Task LogsTheRegistrationWithoutThePassword()
    {
        await CreateRegistration().RegisterFirstAdministratorAsync("alice", "Alice", "alice@example.com", "Sup3r-secret-pass");

        LogRecord record = Assert.Single(_audit.Logger.Records);
        Assert.Equal(9006, record.EventId.Id);
        Assert.Equal(_store.ByUsername("alice").Id, record.Properties["UserId"]);
        Assert.Equal("alice", record.Properties["Username"]);
        Assert.DoesNotContain("Sup3r-secret-pass", record.Message, StringComparison.Ordinal);
        Assert.False(record.Properties.ContainsKey("ActorId"));
    }

    [Fact]
    public async Task LogsNothingWhenTheRegistrationIsClosedOrRejected()
    {
        Registration registration = CreateRegistration();
        await registration.RegisterFirstAdministratorAsync("alice", "Alice", "alice@example.com", "password");
        _audit.Logger.Records.Clear();

        await registration.RegisterFirstAdministratorAsync("bob", "Bob", "bob@example.com", "password");

        Assert.Empty(_audit.Logger.Records);
    }

    [Fact]
    public async Task ClosesAfterTheFirstUser()
    {
        Registration registration = CreateRegistration();
        await registration.RegisterFirstAdministratorAsync("alice", "Alice", "alice@example.com", "password");

        RegistrationResult result = await registration.RegisterFirstAdministratorAsync("bob", "Bob", "bob@example.com", "password");

        Assert.False(await registration.IsOpenAsync());
        Assert.Equal(RegistrationStatus.Closed, result.Status);
        Assert.Null(await _store.FindByUsernameAsync("bob"));
    }
}
