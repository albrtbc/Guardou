namespace Guardou.Infrastructure.Previews;

internal sealed class VipsImageSource : IImageSource
{
    public bool Supports(string extension)
    {
        return ImageGuard.IsDecodable(extension);
    }

    public Task<SourceImage> ReadAsync(string path, string extension, CancellationToken cancellationToken)
    {
        return Task.FromResult(SourceImage.FromFile(path, extension));
    }
}
