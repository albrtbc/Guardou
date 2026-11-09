using Guardou.Domain.Previews;
using Guardou.Infrastructure.Files;
using Guardou.Infrastructure.Previews;
using Guardou.Infrastructure.Storage;
using Guardou.UnitTests.Files;
using NetVips;

namespace Guardou.UnitTests.Previews;

public sealed class VipsPreviewRendererTests : IDisposable
{
    private static readonly DateTimeOffset Modified = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private readonly TemporaryDirectory _directory = new();
    private readonly StoragePaths _paths;
    private readonly LocalBlobStore _blobs;
    private readonly VipsPreviewRenderer _renderer;

    public VipsPreviewRendererTests()
    {
        _paths = new StoragePaths(_directory.Path);
        _blobs = new LocalBlobStore(_paths);
        _renderer = new VipsPreviewRenderer(
            [new VipsImageSource(), new RawImageSource(), new HeicImageSource(_paths), new PdfImageSource(_paths), new VideoImageSource()],
            _blobs);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Theory]
    [InlineData("photo.JPG", true)]
    [InlineData("image.webp", true)]
    [InlineData("camera.cr2", true)]
    [InlineData("notes.txt", false)]
    [InlineData("archive", false)]
    public void SupportsImagesAndRawFiles(string fileName, bool supported)
    {
        Assert.Equal(supported, _renderer.Supports(fileName));
    }

    [Theory]
    [InlineData("photo.heic", "heif-dec")]
    [InlineData("document.pdf", "pdftoppm")]
    [InlineData("movie.mp4", "ffmpeg")]
    public void SupportsOtherFormatsOnlyWithTheTool(string fileName, string tool)
    {
        Assert.Equal(new ExternalTool(tool).IsAvailable, _renderer.Supports(fileName));
    }

    [Fact]
    public async Task RendersWebpPreviewsWithoutUpscaling()
    {
        IReadOnlyDictionary<PreviewSize, byte[]> previews = await RenderAsync(TestImages.Encode(800, 400, ".png"), "wide.png");

        Assert.Equal((400, 200), Dimensions(previews[PreviewSize.Thumbnail]));
        Assert.Equal((800, 400), Dimensions(previews[PreviewSize.Large]));
        Assert.All(previews.Values, preview => Assert.Equal("RIFF"u8.ToArray(), preview[..4]));
    }

    [Fact]
    public async Task AppliesTheExifOrientation()
    {
        byte[] jpeg = TestImages.Encode(800, 400, ".jpg", orientation: 6);

        IReadOnlyDictionary<PreviewSize, byte[]> previews = await RenderAsync(jpeg, "rotated.jpg");

        Assert.Equal((200, 400), Dimensions(previews[PreviewSize.Thumbnail]));
    }

    [Fact]
    public async Task RendersTheEmbeddedJpegOfRawFilesWithTheRawOrientation()
    {
        byte[] raw = [.. TiffHeader.LittleEndian(orientation: 6), .. TestImages.Encode(800, 400, ".jpg")];

        IReadOnlyDictionary<PreviewSize, byte[]> previews = await RenderAsync(raw, "camera.nef");

        Assert.Equal((200, 400), Dimensions(previews[PreviewSize.Thumbnail]));
        Assert.Equal((400, 800), Dimensions(previews[PreviewSize.Large]));
    }

    [Fact]
    public async Task RefusesRawFilesWithoutAPreview()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => RenderAsync(TiffHeader.LittleEndian(1), "camera.dng"));
    }

    [Fact]
    public async Task RendersTheFirstPageOfAPdf()
    {
        Assert.SkipUnless(new ExternalTool("pdftoppm").IsAvailable, "pdftoppm is not installed.");

        IReadOnlyDictionary<PreviewSize, byte[]> previews = await RenderAsync(TestDocuments.Pdf(600, 800), "document.pdf");

        Assert.Equal((300, 400), Dimensions(previews[PreviewSize.Thumbnail]));
        (int width, int height) = Dimensions(previews[PreviewSize.Large]);
        Assert.InRange(width, 1439, 1441);
        Assert.Equal(1920, height);
    }

    [Fact]
    public async Task RendersAFrameOfAVideo()
    {
        ExternalTool ffmpeg = new("ffmpeg");
        Assert.SkipUnless(ffmpeg.IsAvailable, "ffmpeg is not installed.");
        string video = _directory.Combine("movie.mp4");
        await ffmpeg.RunAsync(
            ["-v", "error", "-f", "lavfi", "-i", "testsrc=duration=2:size=640x360:rate=10", "-pix_fmt", "yuv420p", video],
            CancellationToken);

        IReadOnlyDictionary<PreviewSize, byte[]> previews = await RenderAsync(await File.ReadAllBytesAsync(video, CancellationToken), "movie.mp4");

        Assert.Equal((400, 225), Dimensions(previews[PreviewSize.Thumbnail]));
        Assert.Equal((640, 360), Dimensions(previews[PreviewSize.Large]));
    }

    [Fact]
    public async Task RendersHeicImages()
    {
        ExternalTool encoder = new("heif-enc");
        Assert.SkipUnless(encoder.IsAvailable && new ExternalTool("heif-dec").IsAvailable, "libheif tools are not installed.");
        string png = _directory.Combine("source.png");
        string heic = _directory.Combine("photo.heic");
        await File.WriteAllBytesAsync(png, TestImages.Encode(800, 400, ".png"), CancellationToken);
        await encoder.RunAsync([png, "-o", heic], CancellationToken);

        IReadOnlyDictionary<PreviewSize, byte[]> previews = await RenderAsync(await File.ReadAllBytesAsync(heic, CancellationToken), "photo.heic");

        Assert.Equal((400, 200), Dimensions(previews[PreviewSize.Thumbnail]));
    }

    [Fact]
    public async Task ReportsToolErrors()
    {
        Assert.SkipUnless(new ExternalTool("pdftoppm").IsAvailable, "pdftoppm is not installed.");

        await Assert.ThrowsAsync<InvalidDataException>(() => RenderAsync("not a pdf"u8.ToArray(), "broken.pdf"));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_paths.TemporaryDirectory));
    }

    [Fact]
    public async Task RefusesImagesOverThePixelLimit()
    {
        byte[] huge = TestImages.EncodeLarge(20_000, 20_000, ".png");

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() => RenderAsync(huge, "huge.png"));

        Assert.Contains("20000 x 20000", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefusesAnSvgNamedAsAPng()
    {
        byte[] svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\"/></svg>"u8.ToArray();

        await Assert.ThrowsAsync<InvalidDataException>(() => RenderAsync(svg, "drawing.png"));
    }

    [Theory]
    [InlineData(".tif", "photo.jpg", "tiffload")]
    [InlineData(".tif", "photo.png", "tiffload")]
    [InlineData(".tif", "photo.webp", "tiffload")]
    [InlineData(".jpg", "photo.tiff", "jpegload")]
    [InlineData(".png", "photo.tif", "pngload")]
    [InlineData(".jpg", "photo.avif", "jpegload")]
    public async Task RefusesContentWhoseLoaderIsOutsideTheExtensionFamily(string actual, string fileName, string loader)
    {
        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => RenderAsync(TestImages.Encode(100, 50, actual), fileName));

        Assert.Contains($"'{loader}'", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".jpg", "photo.jpe")]
    [InlineData(".png", "photo.jpg")]
    [InlineData(".webp", "photo.png")]
    [InlineData(".jpg", "photo.gif")]
    [InlineData(".tif", "photo.TIF")]
    [InlineData(".gif", "animation.gif")]
    public async Task RendersWebImagesWhateverTheirWebExtensionAndTiffUnderItsOwn(string actual, string fileName)
    {
        IReadOnlyDictionary<PreviewSize, byte[]> previews = await RenderAsync(TestImages.Encode(100, 50, actual), fileName);

        Assert.Equal((100, 50), Dimensions(previews[PreviewSize.Large]));
    }

    [Fact]
    public async Task RendersUltraHdrJpegs()
    {
        byte[] ultraHdr = TestImages.EncodeUltraHdr(800, 400);

        IReadOnlyDictionary<PreviewSize, byte[]> previews = await RenderAsync(ultraHdr, "pixel.jpg");

        Assert.Equal((400, 200), Dimensions(previews[PreviewSize.Thumbnail]));
        Assert.Equal((800, 400), Dimensions(previews[PreviewSize.Large]));
    }

    [Fact]
    public async Task RendersEveryPreviewSizeFromOneDecode()
    {
        IReadOnlyDictionary<PreviewSize, byte[]> previews = await RenderAsync(TestImages.Encode(4000, 3000, ".jpg"), "large.jpg");

        Assert.Equal((400, 300), Dimensions(previews[PreviewSize.Thumbnail]));
        Assert.Equal((1920, 1440), Dimensions(previews[PreviewSize.Large]));
    }

    private static (int Width, int Height) Dimensions(byte[] image)
    {
        using Image decoded = Image.NewFromBuffer(image);
        return (decoded.Width, decoded.Height);
    }

    private async Task<IReadOnlyDictionary<PreviewSize, byte[]>> RenderAsync(byte[] content, string fileName)
    {
        string key = $"owner/2026/01/02/{Guid.NewGuid():N}.{Path.GetExtension(fileName).TrimStart('.')}";
        using MemoryStream source = new(content);
        await _blobs.WriteAsync(key, source, Modified, CancellationToken);
        return await _renderer.RenderAsync(key, fileName, CancellationToken);
    }
}
