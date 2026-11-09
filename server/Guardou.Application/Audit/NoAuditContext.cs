namespace Guardou.Application.Audit;

internal sealed class NoAuditContext : IAuditContext
{
    public string? ActorId => null;

    public string? ClientIp => null;
}
