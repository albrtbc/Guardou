using Guardou.Application.Audit;
using Guardou.Application.Settings;
using Guardou.Domain.Authentication;

namespace Guardou.Application.Authentication;

public sealed class OidcSettingsAdministration(
    EffectiveSettings<OidcSettings> settings,
    ServerSettingsAdministration server,
    IOidcSettingsObserver observer,
    AuditLog audit)
{
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        observer.Changed(await settings.GetAsync(cancellationToken));
    }

    public async Task<OidcSettingsView> GetAsync(string requestOrigin, CancellationToken cancellationToken)
    {
        return await ViewOfAsync(await settings.GetAsync(cancellationToken), requestOrigin, cancellationToken);
    }

    public Task<OidcSettingsView> DefaultsAsync(string requestOrigin, CancellationToken cancellationToken)
    {
        return ViewOfAsync(settings.Defaults, requestOrigin, cancellationToken);
    }

    public async Task<OidcSettingsView> ChangeAsync(
        OidcSettingsChange change,
        string requestOrigin,
        CancellationToken cancellationToken)
    {
        OidcSettings stored = await settings.StoredAsync(cancellationToken);
        OidcSettings requested = Normalize(change, stored.ClientSecret);
        OidcSettings effective = Validate(settings.Apply(requested));
        await SaveAsync(requested, cancellationToken);
        return await ViewOfAsync(effective, requestOrigin, cancellationToken);
    }

    public async Task EnablePasswordLoginAsync(CancellationToken cancellationToken)
    {
        OidcSettings stored = await settings.StoredAsync(cancellationToken);
        await SaveAsync(stored with { PasswordLogin = true }, cancellationToken);
    }

    private async Task SaveAsync(OidcSettings requested, CancellationToken cancellationToken)
    {
        await settings.SaveAsync(requested, cancellationToken);
        audit.SettingsChanged(AuditSections.Oidc);
        observer.Changed(await settings.GetAsync(cancellationToken));
    }

    private async Task<OidcSettingsView> ViewOfAsync(OidcSettings effective, string requestOrigin, CancellationToken cancellationToken)
    {
        string origin = await server.PublicOriginAsync(requestOrigin, cancellationToken);
        return new OidcSettingsView(
            effective.Issuer,
            effective.ClientId,
            effective.ClientSecret is not null,
            effective.Scopes,
            effective.UserClaim,
            effective.AutoCreateUsers,
            effective.PasswordLogin,
            OidcEndpoints.Callback(origin),
            OidcEndpoints.SignedOut(origin),
            settings.Locked);
    }

    private static OidcSettings Normalize(OidcSettingsChange change, string? currentSecret)
    {
        return new OidcSettings(
            OidcSettingsSchema.Issuer(change.Issuer),
            SettingsText.Optional(change.ClientId),
            change.ClientSecret is null ? currentSecret : SettingsText.Optional(change.ClientSecret),
            OidcSettingsSchema.Scopes(change.Scopes),
            SettingsText.Required(change.UserClaim, "user claim"),
            change.AutoCreateUsers,
            change.PasswordLogin);
    }

    private static OidcSettings Validate(OidcSettings effective)
    {
        if ((effective.Issuer is null) != (effective.ClientId is null))
        {
            throw new InvalidSettingsException("The issuer and the client id must be set together.");
        }

        return effective.AllowsPasswordLogin == effective.PasswordLogin
            ? effective
            : throw new InvalidSettingsException("The password login can be disabled only when OIDC is configured.");
    }
}
