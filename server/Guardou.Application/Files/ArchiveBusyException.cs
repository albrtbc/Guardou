namespace Guardou.Application.Files;

public sealed class ArchiveBusyException()
    : Exception("The server is busy preparing other archives. Try again shortly.")
{
    public const int RetryAfterSeconds = 10;
}
