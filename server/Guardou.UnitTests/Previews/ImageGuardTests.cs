using Guardou.Infrastructure.Previews;
using NetVips;

namespace Guardou.UnitTests.Previews;

public sealed class ImageGuardTests
{
    [Fact]
    public void AcceptsAnImageAtThePixelLimit()
    {
        using Image header = Image.Black(10_000, 10_000);

        ImageGuard.CheckPixels(header);
    }

    [Fact]
    public void RefusesAnImageOneRowOverThePixelLimit()
    {
        using Image header = Image.Black(10_000, 10_001);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ImageGuard.CheckPixels(header));

        Assert.Contains("100 megapixels", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CountsPixelsWithoutOverflowingAnInt()
    {
        using Image header = Image.Black(70_000, 70_000);

        Assert.Throws<InvalidDataException>(() => ImageGuard.CheckPixels(header));
    }

    [Theory]
    [InlineData("jpg")]
    [InlineData("JPEG")]
    [InlineData("jpe")]
    [InlineData("png")]
    [InlineData("gif")]
    [InlineData("webp")]
    [InlineData("tif")]
    [InlineData("tiff")]
    [InlineData("avif")]
    public void DecodesTheFormatsTheAppPreviews(string extension)
    {
        Assert.True(ImageGuard.IsDecodable(extension));
    }

    [Theory]
    [InlineData("svg")]
    [InlineData("heic")]
    [InlineData("pdf")]
    [InlineData("bmp")]
    public void DoesNotDecodeOtherFormatsWithLibvipsDirectly(string extension)
    {
        Assert.False(ImageGuard.IsDecodable(extension));
    }

    [Fact]
    public void ChecksTheLoaderFamilyOfTheExtension()
    {
        using Image jpeg = Image.NewFromBuffer(TestImages.Encode(10, 10, ".jpg"));
        using Image tiff = Image.NewFromBuffer(TestImages.Encode(10, 10, ".tif"));

        ImageGuard.Check(jpeg, "png");
        ImageGuard.Check(tiff, "tif");
        Assert.Throws<InvalidDataException>(() => ImageGuard.Check(tiff, "jpg"));
        Assert.Throws<InvalidDataException>(() => ImageGuard.Check(jpeg, "tiff"));
        Assert.Throws<InvalidDataException>(() => ImageGuard.Check(jpeg, "avif"));
    }
}
