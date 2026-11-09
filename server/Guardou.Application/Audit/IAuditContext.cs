namespace Guardou.Application.Audit;

/// <summary>
/// Who is acting and from where, for the audit log. The web host fills it from the current request; the CLI has no request.
/// </summary>
public interface IAuditContext
{
    string? ActorId { get; }

    string? ClientIp { get; }
}
