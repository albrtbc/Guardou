using Guardou.Application.Audit;

namespace Guardou.Cli.Hosting;

// The CLI has no signed-in user and no client address, so its changes are attributed to "cli".
internal sealed class CliAuditContext : IAuditContext
{
    public const string Actor = "cli";

    public string? ActorId => Actor;

    public string? ClientIp => null;
}
