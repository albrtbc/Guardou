using Guardou.Infrastructure.Previews;
using Guardou.UnitTests.Files;

namespace Guardou.UnitTests.Previews;

public sealed class SourceFileTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Fact]
    public async Task ReadsAFileWithinTheMaximum()
    {
        string path = _directory.Combine("small.bin");
        await File.WriteAllBytesAsync(path, new byte[10], CancellationToken);

        Assert.Equal(10, (await SourceFile.ReadAsync(path, CancellationToken, maximumBytes: 10)).Length);
    }

    [Fact]
    public async Task RefusesAFileOverTheMaximum()
    {
        string path = _directory.Combine("large.bin");
        await File.WriteAllBytesAsync(path, new byte[11], CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => SourceFile.ReadAsync(path, CancellationToken, maximumBytes: 10));
    }
}
