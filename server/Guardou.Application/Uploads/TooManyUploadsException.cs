using Guardou.Domain.Files;

namespace Guardou.Application.Uploads;

public sealed class TooManyUploadsException()
    : Exception($"No more than {StoragePolicy.MaximumUploadsInProgress} uploads can be in progress.")
{
    public const string Code = "tooManyUploads";
}
