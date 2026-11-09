using Guardou.Infrastructure.Storage;

namespace Guardou.UnitTests.Storage;

public sealed class StorageCapacityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "guardou-tests", Guid.NewGuid().ToString("N"), "data");

    public void Dispose()
    {
        string parent = Path.GetDirectoryName(_root)!;
        if (Directory.Exists(parent))
        {
            Directory.Delete(parent, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void MeasuresTheVolumeEvenBeforeTheFilesDirectoryExists()
    {
        long available = new StorageCapacity(new StoragePaths(_root)).AvailableBytes();

        Assert.True(available > 0);
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public void UsesTheFilesDirectoryWhenItExists()
    {
        string files = Directory.CreateDirectory(new StoragePaths(_root).FilesDirectory).FullName;

        Assert.Equal(files, StorageCapacity.ExistingAncestor(files));
    }

    [Fact]
    public void UsesTheRootWhenOnlyTheRootExists()
    {
        string root = Directory.CreateDirectory(_root).FullName;

        Assert.Equal(root, StorageCapacity.ExistingAncestor(new StoragePaths(_root).FilesDirectory));
    }

    [Fact]
    public void UsesTheNearestExistingParentWhenNothingExists()
    {
        string parent = Directory.CreateDirectory(Path.GetDirectoryName(_root)!).FullName;

        Assert.Equal(parent, StorageCapacity.ExistingAncestor(new StoragePaths(_root).FilesDirectory));
    }
}
