namespace Guardou.Domain.Files;

public sealed class Item
{
    private Item(
        Guid id,
        string ownerId,
        string createdBy,
        Guid? parentId,
        string name,
        ItemKind kind,
        long size,
        string? storageKey,
        DateTimeOffset created,
        DateTimeOffset modified,
        DateTimeOffset? trashed)
    {
        Id = id;
        OwnerId = ownerId;
        CreatedBy = createdBy;
        ParentId = parentId;
        Name = name;
        Kind = kind;
        Size = size;
        StorageKey = storageKey;
        Created = created;
        Modified = modified;
        Trashed = trashed;
    }

    public Guid Id { get; private set; }

    public string OwnerId { get; private set; }

    public string CreatedBy { get; private set; }

    public Guid? ParentId { get; private set; }

    public string Name { get; private set; }

    public ItemKind Kind { get; private set; }

    public long Size { get; private set; }

    public string? StorageKey { get; private set; }

    public DateTimeOffset Created { get; private set; }

    public DateTimeOffset Modified { get; private set; }

    public DateTimeOffset? Trashed { get; private set; }

    public bool Starred { get; private set; }

    public bool IsRoot => ParentId is null;

    public bool IsFolder => Kind == ItemKind.Folder;

    public static Item CreateRoot(string ownerId, DateTimeOffset now)
    {
        return new Item(Guid.CreateVersion7(now), ownerId, ownerId, null, string.Empty, ItemKind.Folder, 0, null, now, now, null);
    }

    public static Item CreateFolder(Item parent, string name, string createdBy, DateTimeOffset now)
    {
        return new Item(Guid.CreateVersion7(now), parent.OwnerId, createdBy, parent.Id, name, ItemKind.Folder, 0, null, now, now, null);
    }

    public static Item CreateFile(Item parent, StoredFile file, string createdBy, DateTimeOffset now)
    {
        return new Item(
            file.Id,
            parent.OwnerId,
            createdBy,
            parent.Id,
            file.Name,
            ItemKind.File,
            file.Size,
            file.StorageKey,
            now,
            file.Modified,
            null);
    }

    public void Rename(string name, DateTimeOffset now)
    {
        Name = name;
        Modified = now;
    }

    public void ReplaceName(string name)
    {
        Name = name;
    }

    public void MoveTo(Item parent, string name)
    {
        ParentId = parent.Id;
        Name = name;
    }

    public void Star()
    {
        Starred = true;
    }

    public void Unstar()
    {
        Starred = false;
    }

    public void Trash(DateTimeOffset now)
    {
        Trashed = now;
    }

    public void Restore(Item parent, string name)
    {
        ParentId = parent.Id;
        Name = name;
        Trashed = null;
    }
}
