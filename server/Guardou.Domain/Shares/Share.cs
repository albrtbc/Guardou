namespace Guardou.Domain.Shares;

public sealed class Share
{
    private Share(Guid id, Guid itemId, string token, DateTimeOffset created)
    {
        Id = id;
        ItemId = itemId;
        Token = token;
        Created = created;
    }

    public Guid Id { get; private set; }

    public Guid ItemId { get; private set; }

    public string Token { get; private set; }

    public DateTimeOffset Created { get; private set; }

    public DateTimeOffset? Expires { get; private set; }

    public static Share Create(Guid itemId, DateTimeOffset now)
    {
        return new Share(Guid.CreateVersion7(now), itemId, ShareToken.Create(), now);
    }

    public bool IsActive(DateTimeOffset now)
    {
        return Expires is null || Expires > now;
    }

    public void ExpireAt(DateTimeOffset? expires)
    {
        Expires = expires;
    }
}
