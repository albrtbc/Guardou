using Guardou.Application.Audit;
using Guardou.Domain.Files;
using Guardou.UnitTests.Files;
using Microsoft.Extensions.Logging;

namespace Guardou.UnitTests.Audit;

public sealed class AuditLogTests
{
    private readonly TestAudit _audit = new();

    private IReadOnlyList<LogRecord> Records => _audit.Logger.Records;

    [Fact]
    public void LogsAFailedSignInWithTheUsernameAndTheAddress()
    {
        _audit.Log.SignInFailed("alice");

        LogRecord record = Assert.Single(Records);
        Assert.Equal(9002, record.EventId.Id);
        Assert.Equal("SignInFailed", record.EventId.Name);
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal("alice", record.Properties["Username"]);
        Assert.Equal(TestAudit.Ip, record.Properties["ClientIp"]);
    }

    [Fact]
    public void LogsALockoutWithItsOwnEvent()
    {
        _audit.Log.LockoutStarted("user-1", "alice");
        _audit.Log.SignInRefusedLockedOut("user-1", "alice");

        Assert.Equal([9003, 9004], Records.Select(record => record.EventId.Id));
        Assert.All(Records, record => Assert.Equal(LogLevel.Warning, record.Level));
    }

    [Fact]
    public void KeepsControlCharactersOutOfTheLog()
    {
        _audit.Log.SignInFailed("alice\r\n[Audit] forged event");

        LogRecord record = Assert.Single(Records);
        Assert.DoesNotContain('\n', record.Message);
        Assert.DoesNotContain('\r', record.Message);
        Assert.Equal("alice??[Audit] forged event", record.Properties["Username"]);
    }

    [Theory]
    [InlineData("alice\u2028forged", "alice?forged")]
    [InlineData("alice\u2029forged", "alice?forged")]
    [InlineData("alice\u202Eevil", "alice?evil")]
    [InlineData("alice\u200Bbob", "alice?bob")]
    [InlineData("alice\u0085next", "alice?next")]
    [InlineData("alice \U0001F600 b\u00E9", "alice \U0001F600 b\u00E9")]
    public void StripsLineSeparatorsAndFormatCharacters(string username, string expected)
    {
        _audit.Log.SignInFailed(username);

        Assert.Equal(expected, Records.Single().Properties["Username"]);
    }

    [Fact]
    public void CutsALongUsername()
    {
        _audit.Log.SignInFailed(new string('a', 1000));

        Assert.Equal(128, ((string)Records.Single().Properties["Username"]!).Length);
    }

    [Fact]
    public void LogsAnonymousAccessWithTheShareTheItemTheActionAndTheAddress()
    {
        Guid shareId = Guid.CreateVersion7();
        Guid itemId = Guid.CreateVersion7();

        _audit.Log.SharedItemAccessed(shareId, itemId, ShareAccessKind.Download);

        LogRecord record = Assert.Single(Records);
        Assert.Equal(9040, record.EventId.Id);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(shareId, record.Properties["ShareId"]);
        Assert.Equal(ItemId.Format(itemId), record.Properties["ItemId"]);
        Assert.Equal(ShareAccessKind.Download, record.Properties["Action"]);
        Assert.Equal(TestAudit.Ip, record.Properties["ClientIp"]);
        Assert.Equal(TestAudit.Actor, record.Properties["ActorId"]);
    }

    [Fact]
    public void LeavesTheActorEmptyForAnAnonymousVisitor()
    {
        TestAudit anonymous = new(actorId: null);

        anonymous.Log.SharedItemAccessed(Guid.CreateVersion7(), Guid.CreateVersion7(), ShareAccessKind.View);

        LogRecord record = Assert.Single(anonymous.Logger.Records);
        Assert.Null(record.Properties["ActorId"]);
        Assert.DoesNotContain("anonymously", record.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ShareAccessKind.Preview)]
    [InlineData(ShareAccessKind.CreatorPicture)]
    public void LogsPreviewsAtDebug(ShareAccessKind action)
    {
        _audit.Log.SharedItemAccessed(Guid.CreateVersion7(), Guid.CreateVersion7(), action);

        LogRecord record = Assert.Single(Records);
        Assert.Equal(9041, record.EventId.Id);
        Assert.Equal(LogLevel.Debug, record.Level);
        Assert.Equal(TestAudit.Actor, record.Properties["ActorId"]);
    }

    [Theory]
    [InlineData(ShareAccessKind.Open)]
    [InlineData(ShareAccessKind.Details)]
    [InlineData(ShareAccessKind.Download)]
    [InlineData(ShareAccessKind.View)]
    [InlineData(ShareAccessKind.Archive)]
    public void LogsEveryOtherAnonymousAccessAtInformation(ShareAccessKind action)
    {
        _audit.Log.SharedItemAccessed(Guid.CreateVersion7(), Guid.CreateVersion7(), action);

        Assert.Equal(LogLevel.Information, Assert.Single(Records).Level);
    }

    [Fact]
    public void LogsShareChangesWithTheActor()
    {
        Guid shareId = Guid.CreateVersion7();
        Guid itemId = Guid.CreateVersion7();

        _audit.Log.ShareLinkCreated(shareId, itemId);
        _audit.Log.ShareLinkRevoked(shareId, itemId);
        _audit.Log.DirectShareAdded(itemId, "user-2");
        _audit.Log.DirectShareRemoved(itemId, "user-2");

        Assert.Equal([9030, 9031, 9032, 9033], Records.Select(record => record.EventId.Id));
        Assert.All(Records, record => Assert.Equal(TestAudit.Actor, record.Properties["ActorId"]));
    }

    [Fact]
    public void LogsAnExpiryChangeWithTheActorTheItemAndTheDate()
    {
        Guid shareId = Guid.CreateVersion7();
        Guid itemId = Guid.CreateVersion7();
        DateTimeOffset expires = new(2027, 1, 2, 3, 4, 5, TimeSpan.FromHours(2));

        _audit.Log.ShareLinkExpiryChanged(shareId, itemId, expires);

        LogRecord record = Assert.Single(Records);
        Assert.Equal(9034, record.EventId.Id);
        Assert.Equal("ShareLinkExpiryChanged", record.EventId.Name);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(TestAudit.Actor, record.Properties["ActorId"]);
        Assert.Equal(TestAudit.Ip, record.Properties["ClientIp"]);
        Assert.Equal(shareId, record.Properties["ShareId"]);
        Assert.Equal(ItemId.Format(itemId), record.Properties["ItemId"]);
        Assert.Equal("2027-01-02T01:04:05.0000000Z", record.Properties["Expires"]);
        Assert.Contains("expiry set to 2027-01-02T01:04:05.0000000Z by", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LogsARemovedExpiryAsNever()
    {
        _audit.Log.ShareLinkExpiryChanged(Guid.CreateVersion7(), Guid.CreateVersion7(), expires: null);

        LogRecord record = Assert.Single(Records);
        Assert.Equal(9034, record.EventId.Id);
        Assert.Equal("never", record.Properties["Expires"]);
        Assert.Contains("expiry set to never by", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UsesTheAuditCategory()
    {
        Assert.Equal("Guardou.Audit", AuditLog.Category);
    }
}
