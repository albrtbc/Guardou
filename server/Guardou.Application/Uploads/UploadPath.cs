using Guardou.Application.Files;

namespace Guardou.Application.Uploads;

internal static class UploadPath
{
    private static readonly char[] Separators = ['/', '\\'];

    public static IReadOnlyList<string> FolderNames(string? relativePath, int destinationDepth)
    {
        string[] segments = (relativePath ?? string.Empty).Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        string[] folders = segments.SkipLast(1).ToArray();
        FolderDepth.EnsureWithin(destinationDepth + folders.Length);
        return folders.Select(ItemPlacement.ValidName).ToList();
    }
}
