namespace Guardou.Application.Settings;

public sealed record ServerSettingsView(
    string? ExternalUrl,
    int TrashRetentionDays,
    int UploadConcurrency,
    int UploadChunkSizeMb,
    int? MaxFileSizeMb,
    int MinFreeSpaceMb,
    IReadOnlyList<LockedSetting> Locked);
