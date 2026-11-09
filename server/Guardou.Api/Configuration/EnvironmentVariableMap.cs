using Guardou.Application.Settings;

namespace Guardou.Api.Configuration;

internal static class EnvironmentVariableMap
{
    public static IReadOnlyDictionary<string, string> Keys { get; } = new Dictionary<string, string>
    {
        [SettingsVariables.ExternalUrl] = Override(nameof(SettingsOverrides.ExternalUrl)),
        [SettingsVariables.TrashRetentionDays] = Override(nameof(SettingsOverrides.TrashRetentionDays)),
        [SettingsVariables.UploadConcurrency] = Override(nameof(SettingsOverrides.UploadConcurrency)),
        [SettingsVariables.UploadChunkSizeMb] = Override(nameof(SettingsOverrides.UploadChunkSizeMb)),
        [SettingsVariables.MaxFileSizeMb] = Override(nameof(SettingsOverrides.MaxFileSizeMb)),
        [SettingsVariables.MinFreeSpaceMb] = Override(nameof(SettingsOverrides.MinFreeSpaceMb)),
        [SettingsVariables.OidcIssuer] = Override(nameof(SettingsOverrides.OidcIssuer)),
        [SettingsVariables.OidcClientId] = Override(nameof(SettingsOverrides.OidcClientId)),
        [SettingsVariables.OidcClientSecret] = Override(nameof(SettingsOverrides.OidcClientSecret)),
        [SettingsVariables.OidcScopes] = Override(nameof(SettingsOverrides.OidcScopes)),
        [SettingsVariables.OidcUserClaim] = Override(nameof(SettingsOverrides.OidcUserClaim)),
        [TrustedProxies.Variable] = TrustedProxies.Key,
    };

    private static string Override(string property)
    {
        return $"{SettingsOverrides.Section}:{property}";
    }
}
