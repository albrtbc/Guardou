using Guardou.Application.Audit;
using Guardou.UnitTests.Files;

namespace Guardou.UnitTests.Audit;

internal sealed class TestAudit : IAuditContext
{
    public const string Actor = "actor-1";
    public const string Ip = "203.0.113.7";

    public TestAudit(string? actorId = Actor, string? clientIp = Ip)
    {
        ActorId = actorId;
        ClientIp = clientIp;
        Log = new AuditLog(Logger, this);
    }

    public RecordingLogger<AuditLog> Logger { get; } = new();

    public AuditLog Log { get; }

    public string? ActorId { get; }

    public string? ClientIp { get; }
}
