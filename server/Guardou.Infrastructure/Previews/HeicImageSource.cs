using System.Collections.Frozen;
using Guardou.Infrastructure.Storage;

namespace Guardou.Infrastructure.Previews;

internal sealed class HeicImageSource(StoragePaths paths) : IImageSource
{
    private const string OutputExtension = "jpg";
    private const string OutputName = "image." + OutputExtension;
    private const string Quality = "90";
    private const long MaximumOutputBytes = 64L * 1024 * 1024;

    private static readonly FrozenSet<string> Extensions = FrozenSet.Create(StringComparer.OrdinalIgnoreCase, "heic", "heif");

    private readonly ExternalTool _decoder = new("heif-dec");

    public bool Supports(string extension)
    {
        return Extensions.Contains(extension) && _decoder.IsAvailable;
    }

    public async Task<SourceImage> ReadAsync(string path, string extension, CancellationToken cancellationToken)
    {
        using ScratchDirectory scratch = new(paths.TemporaryDirectory);
        string output = scratch.Combine(OutputName);
        await _decoder.RunAsync(["--quality", Quality, path, output], cancellationToken);
        return SourceImage.FromBuffer(await SourceFile.ReadAsync(output, cancellationToken, MaximumOutputBytes), OutputExtension, TiffOrientation.Normal);
    }
}
