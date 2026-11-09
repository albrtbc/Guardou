using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using Guardou.Domain.Previews;

namespace Guardou.Infrastructure.Previews;

internal sealed class VideoImageSource : IImageSource
{
    private const double FramePosition = 0.1;
    private const double MaximumSeekSeconds = 10;
    private const string FrameExtension = "png";

    private static readonly FrozenSet<string> Extensions = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "3gp", "avi", "m2ts", "m4v", "mkv", "mov", "mp4", "mpeg", "mpg", "mts", "webm", "wmv");

    private readonly ExternalTool _probe = new("ffprobe");
    private readonly ExternalTool _decoder = new("ffmpeg");

    public bool Supports(string extension)
    {
        return Extensions.Contains(extension) && _probe.IsAvailable && _decoder.IsAvailable;
    }

    public async Task<SourceImage> ReadAsync(string path, string extension, CancellationToken cancellationToken)
    {
        double seek = SeekSeconds(await DurationAsync(path, cancellationToken));
        byte[] frame = await TryFrameAtAsync(path, seek, cancellationToken);
        if (frame.Length == 0 && seek > 0)
        {
            frame = await FrameAtAsync(path, 0, cancellationToken);
        }

        return frame.Length > 0
            ? SourceImage.FromBuffer(frame, FrameExtension, TiffOrientation.Normal)
            : throw new InvalidDataException("The video has no frame.");
    }

    private async Task<byte[]> TryFrameAtAsync(string path, double seconds, CancellationToken cancellationToken)
    {
        try
        {
            return await FrameAtAsync(path, seconds, cancellationToken);
        }
        catch (InvalidDataException) when (seconds > 0)
        {
            return [];
        }
    }

    private Task<byte[]> FrameAtAsync(string path, double seconds, CancellationToken cancellationToken)
    {
        int edge = PreviewSizes.MaximumEdge(PreviewSize.Large);
        string scale = string.Create(CultureInfo.InvariantCulture, $"scale='min({edge},iw)':'min({edge},ih)':force_original_aspect_ratio=decrease");
        string seek = seconds.ToString("0.###", CultureInfo.InvariantCulture);
        return _decoder.RunAsync(
            ["-v", "error", "-ss", seek, "-i", path, "-frames:v", "1", "-vf", scale, "-f", "image2pipe", "-c:v", "png", "-"],
            cancellationToken);
    }

    private static double SeekSeconds(double duration)
    {
        return Math.Min(Math.Max(duration, 0) * FramePosition, MaximumSeekSeconds);
    }

    private async Task<double> DurationAsync(string path, CancellationToken cancellationToken)
    {
        byte[] output = await _probe.RunAsync(
            ["-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", path],
            cancellationToken);
        return double.TryParse(Encoding.UTF8.GetString(output).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double duration)
            ? duration
            : 0;
    }
}
