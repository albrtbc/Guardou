using System.Collections.Frozen;
using NetVips;

namespace Guardou.Infrastructure.Previews;

internal sealed class RawImageSource : IImageSource
{
    private const string OrientationField = "orientation";
    private const string JpegExtension = "jpg";

    private static readonly FrozenSet<string> Extensions = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "3fr", "arw", "cr2", "cr3", "dng", "erf", "kdc", "mrw", "nef", "nrw", "orf", "pef", "raf", "rw2", "sr2", "srf", "srw", "x3f");

    public bool Supports(string extension)
    {
        return Extensions.Contains(extension);
    }

    public async Task<SourceImage> ReadAsync(string path, string extension, CancellationToken cancellationToken)
    {
        byte[] content = await SourceFile.ReadAsync(path, cancellationToken);
        ReadOnlyMemory<byte> jpeg = EmbeddedJpeg.FindLargest(content)
            ?? throw new InvalidDataException("The RAW file has no embedded JPEG preview.");
        byte[] preview = jpeg.ToArray();
        int orientation = HasOrientation(preview) ? TiffOrientation.Normal : TiffOrientation.Read(content);
        return SourceImage.FromBuffer(preview, JpegExtension, orientation);
    }

    private static bool HasOrientation(byte[] jpeg)
    {
        using Image image = Image.NewFromBuffer(jpeg);
        return image.Contains(OrientationField) && image.Get(OrientationField) is int value && value != TiffOrientation.Normal;
    }
}
