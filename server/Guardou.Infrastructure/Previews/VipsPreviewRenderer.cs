using Guardou.Application.Previews;
using Guardou.Domain.Files;
using Guardou.Domain.Previews;
using Guardou.Infrastructure.Files;
using NetVips;

namespace Guardou.Infrastructure.Previews;

internal sealed class VipsPreviewRenderer(IEnumerable<IImageSource> sources, LocalBlobStore blobs) : IPreviewRenderer
{
    private const int Quality = 80;
    private const string OrientationField = "orientation";

    private readonly IImageSource[] _sources = sources.ToArray();

    static VipsPreviewRenderer()
    {
        VipsDefaults.Apply();
    }

    public bool Supports(string fileName)
    {
        return SourceFor(fileName) is not null;
    }

    public async Task<IReadOnlyDictionary<PreviewSize, byte[]>> RenderAsync(
        string storageKey,
        string fileName,
        CancellationToken cancellationToken)
    {
        string extension = ItemName.Extension(fileName);
        IImageSource source = SourceFor(fileName) ?? throw new InvalidDataException($"The file '{fileName}' has no preview.");
        SourceImage image = await source.ReadAsync(blobs.PathOf(storageKey), extension, cancellationToken);
        return Render(image, cancellationToken);
    }

    private IImageSource? SourceFor(string fileName)
    {
        string extension = ItemName.Extension(fileName);
        return _sources.FirstOrDefault(source => source.Supports(extension));
    }

    private static Dictionary<PreviewSize, byte[]> Render(SourceImage image, CancellationToken cancellationToken)
    {
        Inspect(image);
        cancellationToken.ThrowIfCancellationRequested();
        using Image thumbnail = image.Thumbnail(PreviewSizes.All.Max(PreviewSizes.MaximumEdge));
        using Image decoded = thumbnail.CopyMemory();
        using Image oriented = Orient(decoded, image.Orientation);
        return PreviewSizes.All.ToDictionary(size => size, size => Encode(oriented, size));
    }

    private static void Inspect(SourceImage image)
    {
        try
        {
            using Image header = image.OpenHeader();
            ImageGuard.Check(header, image.Extension);
        }
        catch (VipsException exception)
        {
            throw new InvalidDataException($"The file is not a valid '.{image.Extension}' image, or its format is not allowed.", exception);
        }
    }

    private static byte[] Encode(Image image, PreviewSize size)
    {
        int edge = PreviewSizes.MaximumEdge(size);
        using Image thumbnail = image.ThumbnailImage(edge, height: edge, size: Enums.Size.Down);
        return thumbnail.WebpsaveBuffer(q: Quality, keep: Enums.ForeignKeep.None);
    }

    private static Image Orient(Image image, int orientation)
    {
        if (orientation == TiffOrientation.Normal)
        {
            return image.Copy();
        }

        using Image tagged = image.Mutate(mutable => mutable.Set(GValue.GIntType, OrientationField, orientation));
        return tagged.Autorot();
    }
}
