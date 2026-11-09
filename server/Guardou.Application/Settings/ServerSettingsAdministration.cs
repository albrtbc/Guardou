using Guardou.Application.Audit;
using Guardou.Domain.Settings;

namespace Guardou.Application.Settings;

public sealed class ServerSettingsAdministration(EffectiveSettings<ServerSettings> settings, AuditLog audit)
{
    public async Task<ServerSettingsView> GetAsync(CancellationToken cancellationToken)
    {
        return ViewOf(await settings.GetAsync(cancellationToken));
    }

    public ServerSettingsView Defaults()
    {
        return ViewOf(settings.Defaults);
    }

    public async Task<ServerSettingsView> ChangeAsync(ServerSettings requested, CancellationToken cancellationToken)
    {
        await settings.SaveAsync(ServerSettingsSchema.Normalize(requested), cancellationToken);
        audit.SettingsChanged(AuditSections.Server);
        return await GetAsync(cancellationToken);
    }

    public async Task<string> PublicOriginAsync(string requestOrigin, CancellationToken cancellationToken)
    {
        return (await settings.GetAsync(cancellationToken)).ExternalUrl ?? requestOrigin;
    }

    private ServerSettingsView ViewOf(ServerSettings effective)
    {
        return new ServerSettingsView(
            effective.ExternalUrl,
            effective.TrashRetentionDays,
            effective.UploadConcurrency,
            effective.UploadChunkSizeMb,
            effective.MaxFileSizeMb,
            effective.MinFreeSpaceMb,
            settings.Locked);
    }
}
