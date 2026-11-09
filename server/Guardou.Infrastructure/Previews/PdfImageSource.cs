using System.Globalization;
using Guardou.Domain.Previews;
using Guardou.Infrastructure.Storage;

namespace Guardou.Infrastructure.Previews;

internal sealed class PdfImageSource(StoragePaths paths) : IImageSource
{
    private const string Extension = "pdf";
    private const string OutputRoot = "page";
    private const string OutputExtension = "png";

    private readonly ExternalTool _renderer = new("pdftoppm");

    public bool Supports(string extension)
    {
        return extension.Equals(Extension, StringComparison.OrdinalIgnoreCase) && _renderer.IsAvailable;
    }

    public async Task<SourceImage> ReadAsync(string path, string extension, CancellationToken cancellationToken)
    {
        using ScratchDirectory scratch = new(paths.TemporaryDirectory);
        string root = scratch.Combine(OutputRoot);
        string edge = PreviewSizes.MaximumEdge(PreviewSize.Large).ToString(CultureInfo.InvariantCulture);
        await _renderer.RunAsync(["-png", "-singlefile", "-f", "1", "-l", "1", "-scale-to", edge, path, root], cancellationToken);
        return SourceImage.FromBuffer(await File.ReadAllBytesAsync(root + "." + OutputExtension, cancellationToken), OutputExtension, TiffOrientation.Normal);
    }
}
