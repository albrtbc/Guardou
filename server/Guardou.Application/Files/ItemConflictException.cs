namespace Guardou.Application.Files;

public sealed class ItemConflictException(string message) : Exception(message)
{
    public static ItemConflictException ForName(string name)
    {
        return new ItemConflictException($"An item with the name '{name}' already exists in the destination.");
    }
}
