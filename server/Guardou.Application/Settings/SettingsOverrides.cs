namespace Guardou.Application.Settings;

public sealed class SettingsOverrides
{
    public const string Section = "Overrides";

    public string? ExternalUrl { get; set; }

    public int? TrashRetentionDays { get; set; }

    public int? UploadConcurrency { get; set; }

    public int? UploadChunkSizeMb { get; set; }

    public int? MaxFileSizeMb { get; set; }

    public int? MinFreeSpaceMb { get; set; }

    public string? OidcIssuer { get; set; }

    public string? OidcClientId { get; set; }

    public string? OidcClientSecret { get; set; }

    public string? OidcScopes { get; set; }

    public string? OidcUserClaim { get; set; }
}
