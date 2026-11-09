using System.Collections.Frozen;
using System.Globalization;
using NetVips;

namespace Guardou.Infrastructure.Previews;

internal static class ImageGuard
{
    public const long MaximumPixels = 100_000_000;

    private const string LoaderField = "vips-loader";

    private static readonly FrozenSet<string> WebLoaders = FrozenSet.Create("jpegload", "uhdrload", "pngload", "webpload", "gifload");
    private static readonly FrozenSet<string> TiffLoaders = FrozenSet.Create("tiffload");
    private static readonly FrozenSet<string> HeifLoaders = FrozenSet.Create("heifload");

    private static readonly FrozenDictionary<string, FrozenSet<string>> Loaders = new Dictionary<string, FrozenSet<string>>
    {
        ["avif"] = HeifLoaders,
        ["gif"] = WebLoaders,
        ["jpe"] = WebLoaders,
        ["jpeg"] = WebLoaders,
        ["jpg"] = WebLoaders,
        ["png"] = WebLoaders,
        ["tif"] = TiffLoaders,
        ["tiff"] = TiffLoaders,
        ["webp"] = WebLoaders,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static bool IsDecodable(string extension)
    {
        return Loaders.ContainsKey(extension);
    }

    public static void CheckPixels(Image header)
    {
        long pixels = (long)header.Width * header.Height;
        if (pixels > MaximumPixels)
        {
            throw new InvalidDataException(string.Create(
                CultureInfo.InvariantCulture,
                $"The image is {header.Width} x {header.Height} pixels, which is more than the {MaximumPixels / 1_000_000} megapixels allowed."));
        }
    }

    public static void Check(Image header, string extension)
    {
        CheckPixels(header);
        CheckLoader(header, extension);
    }

    private static void CheckLoader(Image header, string extension)
    {
        string? loader = header.Contains(LoaderField) ? header.Get(LoaderField) as string : null;
        string family = loader?.Split('_')[0] ?? string.Empty;
        if (!Loaders.TryGetValue(extension, out FrozenSet<string>? allowed) || !allowed.Contains(family))
        {
            throw new InvalidDataException($"The content of the '.{extension}' file is a '{family}' image, which is not allowed for that extension.");
        }
    }
}
