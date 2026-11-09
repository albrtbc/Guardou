using NetVips;

namespace Guardou.Infrastructure.Previews;

internal sealed class SourceImage
{
    private readonly Func<Image> _openHeader;
    private readonly Func<int, Image> _thumbnail;

    private SourceImage(string extension, int orientation, Func<Image> openHeader, Func<int, Image> thumbnail)
    {
        Extension = extension;
        Orientation = orientation;
        _openHeader = openHeader;
        _thumbnail = thumbnail;
    }

    public string Extension { get; }

    public int Orientation { get; }

    public static SourceImage FromFile(string path, string extension)
    {
        return new SourceImage(
            extension,
            TiffOrientation.Normal,
            () => Image.NewFromFile(path, access: Enums.Access.Sequential),
            edge => Image.Thumbnail(path, edge, height: edge, size: Enums.Size.Down));
    }

    public static SourceImage FromBuffer(byte[] content, string extension, int orientation)
    {
        return new SourceImage(
            extension,
            orientation,
            () => Image.NewFromBuffer(content, access: Enums.Access.Sequential),
            edge => Image.ThumbnailBuffer(content, edge, height: edge, size: Enums.Size.Down));
    }

    public Image OpenHeader()
    {
        return _openHeader();
    }

    public Image Thumbnail(int edge)
    {
        return _thumbnail(edge);
    }
}
