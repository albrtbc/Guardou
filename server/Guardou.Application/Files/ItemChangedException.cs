namespace Guardou.Application.Files;

public sealed class ItemChangedException() : Exception("The item changed at the same time. Load it again and try again.")
{
    public const string Code = "itemChanged";
}
