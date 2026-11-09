namespace Guardou.Application.Quotas;

public sealed class FileTooLargeException(int maxFileSizeMb)
    : Exception($"A file cannot be larger than {maxFileSizeMb} MB.")
{
    public const string Code = "fileTooLarge";
}
