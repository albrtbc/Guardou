using Guardou.Application.Files;
using Guardou.Application.Uploads;
using Guardou.Domain.Files;

namespace Guardou.UnitTests.Files;

public sealed class FolderDepthTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(TreeLimits.MaximumFolderDepth)]
    public void AllowsDepthsUpToTheLimit(int depth)
    {
        FolderDepth.EnsureWithin(depth);
    }

    [Fact]
    public void RejectsDepthsBeyondTheLimit()
    {
        InvalidItemRequestException error = Assert.Throws<InvalidItemRequestException>(
            () => FolderDepth.EnsureWithin(TreeLimits.MaximumFolderDepth + 1));

        Assert.Contains("64", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CountsUploadFoldersFromTheDestination()
    {
        string path = string.Join('/', Enumerable.Range(0, 10).Select(index => $"f{index}")) + "/file.txt";

        Assert.Equal(10, UploadPath.FolderNames(path, destinationDepth: 54).Count);
        Assert.Throws<InvalidItemRequestException>(() => UploadPath.FolderNames(path, destinationDepth: 55));
    }

    [Fact]
    public void IgnoresTheFileNameAndEmptySegments()
    {
        Assert.Empty(UploadPath.FolderNames(null, destinationDepth: TreeLimits.MaximumFolderDepth));
        Assert.Empty(UploadPath.FolderNames("file.txt", destinationDepth: TreeLimits.MaximumFolderDepth));
        Assert.Equal(["a", "b"], UploadPath.FolderNames("/a\\\\b/file.txt", destinationDepth: 0));
    }
}
