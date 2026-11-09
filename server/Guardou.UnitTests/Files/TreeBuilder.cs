using Guardou.Domain.Files;

namespace Guardou.UnitTests.Files;

internal sealed class TreeBuilder(FakeItemStore store, string ownerId)
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    public Item Root { get; } = Add(store, Item.CreateRoot(ownerId, Now));

    public Item Folder(Item parent, string name)
    {
        return Add(store, Item.CreateFolder(parent, name, ownerId, Now));
    }

    public Item Chain(Item parent, int length)
    {
        Item current = parent;
        for (int level = 0; level < length; level++)
        {
            current = Folder(current, $"level-{level}");
        }

        return current;
    }

    private static Item Add(FakeItemStore store, Item item)
    {
        store.Add(item);
        return item;
    }
}
