namespace Guardou.Application.Quotas;

public sealed class InsufficientStorageException()
    : Exception("The server does not have enough free space.")
{
    public const string Code = "storageFull";
}
