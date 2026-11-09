using Guardou.Domain.Files;

namespace Guardou.Domain.Settings;

public sealed record ServerSettings(
    string? ExternalUrl,
    int TrashRetentionDays = TrashPolicy.DefaultRetentionDays,
    int UploadConcurrency = UploadPolicy.DefaultConcurrency,
    int UploadChunkSizeMb = UploadPolicy.DefaultChunkSizeMb,
    int? MaxFileSizeMb = null,
    int MinFreeSpaceMb = StoragePolicy.DefaultMinFreeSpaceMb) : ISettings<ServerSettings>
{
    public static string Key => "server";

    public static ServerSettings Initial { get; } = new(ExternalUrl: null);

    public TrashPolicy Trash()
    {
        return new TrashPolicy(TrashRetentionDays);
    }
}
