using Guardou.Application.Users;
using Guardou.Infrastructure.Files;
using Guardou.Infrastructure.Previews;
using Guardou.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using NetVips;

namespace Guardou.Infrastructure.Profiles;

internal sealed partial class FileProfilePictureStore(
    StoragePaths paths,
    ProfilePictureDownloader downloader,
    ILogger<FileProfilePictureStore> logger)
    : IProfilePictureStore
{
    private const string FileName = "picture.webp";
    private const string PartialSuffix = ".part";
    private const int Edge = 256;
    private const int Quality = 80;
    private const int MaximumConcurrentEncodings = 2;

    private static readonly SemaphoreSlim Encodings = new(MaximumConcurrentEncodings);

    static FileProfilePictureStore()
    {
        VipsDefaults.Apply();
    }

    public bool Contains(string userId)
    {
        return PathOf(userId) is { } path && File.Exists(path);
    }

    public ProfilePicture? TryOpen(string userId)
    {
        if (PathOf(userId) is not { } path)
        {
            return null;
        }

        try
        {
            FileStream content = new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            return new ProfilePicture(content, File.GetLastWriteTimeUtc(path));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    public async Task ImportAsync(string userId, Uri source, CancellationToken cancellationToken)
    {
        try
        {
            byte[] picture = await EncodeAsync(await downloader.DownloadAsync(source, cancellationToken), cancellationToken);
            await WriteAsync(userId, picture, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidDataException or VipsException)
        {
            LogImportFailed(source, exception);
        }
    }

    public async Task SaveAsync(string userId, Stream content, CancellationToken cancellationToken)
    {
        byte[] picture;
        try
        {
            picture = await EncodeAsync(
                await BoundedStreamReader.ReadAsync(content, ProfilePictureLimits.MaximumBytes, cancellationToken),
                cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidProfilePictureException(exception.Message, exception);
        }
        catch (VipsException exception)
        {
            throw new InvalidProfilePictureException("The picture is not a supported image or is too large.", exception);
        }

        await WriteAsync(userId, picture, cancellationToken);
    }

    public void Delete(string userId)
    {
        string directory = UserDirectory(userId);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<byte[]> EncodeAsync(byte[] source, CancellationToken cancellationToken)
    {
        await Encodings.WaitAsync(cancellationToken);
        try
        {
            return Encode(source);
        }
        finally
        {
            Encodings.Release();
        }
    }

    private static byte[] Encode(byte[] source)
    {
        using (Image header = Image.NewFromBuffer(source, access: Enums.Access.Sequential))
        {
            ImageGuard.CheckPixels(header);
        }

        using Image thumbnail = Image.ThumbnailBuffer(source, Edge, height: Edge, size: Enums.Size.Down, crop: Enums.Interesting.Centre);
        return thumbnail.WebpsaveBuffer(q: Quality, keep: Enums.ForeignKeep.None);
    }

    private async Task WriteAsync(string userId, byte[] picture, CancellationToken cancellationToken)
    {
        string directory = UserDirectory(userId);
        string path = Path.Combine(directory, FileName);
        string partial = path + PartialSuffix;
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllBytesAsync(partial, picture, cancellationToken);
            File.Move(partial, path, overwrite: true);
        }
        catch
        {
            File.Delete(partial);
            throw;
        }
    }

    private string? PathOf(string userId)
    {
        return SafePathResolver.Resolve(paths.ProfileDirectory, userId) is { } directory
            ? Path.Combine(directory, FileName)
            : null;
    }

    private string UserDirectory(string userId)
    {
        return SafePathResolver.Resolve(paths.ProfileDirectory, userId)
            ?? throw new InvalidOperationException($"The user '{userId}' leaves the profile directory.");
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The profile picture '{Source}' could not be imported.")]
    private partial void LogImportFailed(Uri source, Exception exception);
}
