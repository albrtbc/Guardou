using Guardou.Domain.Files;

namespace Guardou.Application.Files;

internal static class FolderDepth
{
    public static void EnsureWithin(int depth)
    {
        if (depth > TreeLimits.MaximumFolderDepth)
        {
            throw new InvalidItemRequestException($"Folders can be nested at most {TreeLimits.MaximumFolderDepth} levels deep.");
        }
    }
}
