using Guardou.Infrastructure.Previews;
using Guardou.UnitTests.Files;
using NetVips;

namespace Guardou.UnitTests.Previews;

public sealed class VipsImageSourceTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Fact]
    public async Task GivesTheFilePathToLibvips()
    {
        string path = Path.Combine(_directory.Path, "photo.png");
        await File.WriteAllBytesAsync(path, TestImages.Encode(800, 400, ".png"), TestContext.Current.CancellationToken);

        SourceImage source = await new VipsImageSource().ReadAsync(path, "png", TestContext.Current.CancellationToken);
        using Image header = source.OpenHeader();
        using Image thumbnail = source.Thumbnail(400);

        Assert.Equal("pngload", header.Get("vips-loader"));
        Assert.Equal((400, 200), (thumbnail.Width, thumbnail.Height));
    }
}
