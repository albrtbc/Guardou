namespace Guardou.Application.Files;

public sealed class ItemConflictException : Exception
{
    public ItemConflictException(string name)
        : this(name, $"An item with the name '{name}' already exists in the destination.")
    {
    }

    public ItemConflictException(string name, string message)
        : base(message)
    {
        Name = name;
    }

    public string Name { get; }
}
