using Guardou.Application.Audit;
using Guardou.Application.Files;
using Guardou.Application.Settings;
using Guardou.Domain.Files;
using Guardou.Domain.Shares;

namespace Guardou.Application.Shares;

public sealed class ItemSharing
{
    private const string LinkPath = "/s/";

    private readonly IShareStore _shares;
    private readonly OwnedItems _items;
    private readonly ServerSettingsAdministration _server;
    private readonly TimeProvider _time;
    private readonly AuditLog _audit;

    internal ItemSharing(IShareStore shares, OwnedItems items, ServerSettingsAdministration server, TimeProvider time, AuditLog audit)
    {
        _shares = shares;
        _items = items;
        _server = server;
        _time = time;
        _audit = audit;
    }

    public async Task<ShareLink> GetAsync(string ownerId, string id, string requestOrigin, CancellationToken cancellationToken)
    {
        Item item = await _items.FindShareableAsync(ownerId, id, cancellationToken);
        Share share = await _shares.FindByItemAsync(item.Id, cancellationToken)
            ?? throw new ShareNotFoundException($"The item '{id}' is not shared.");
        return await LinkOfAsync(share, requestOrigin, cancellationToken);
    }

    public async Task<ShareLink> ShareAsync(string ownerId, string id, string requestOrigin, CancellationToken cancellationToken)
    {
        Item item = await _items.FindShareableAsync(ownerId, id, cancellationToken);
        Share? share = await _shares.FindByItemAsync(item.Id, cancellationToken);
        if (share is null)
        {
            share = Share.Create(item.Id, _time.GetUtcNow());
            _shares.Add(share);
            await _shares.SaveAsync(cancellationToken);
            _audit.ShareLinkCreated(share.Id, item.Id);
        }

        return await LinkOfAsync(share, requestOrigin, cancellationToken);
    }

    public async Task<ShareLink> ChangeExpiryAsync(
        string ownerId,
        string id,
        ShareExpiry expiry,
        string requestOrigin,
        CancellationToken cancellationToken)
    {
        if (expiry.Expires <= _time.GetUtcNow())
        {
            throw new InvalidItemRequestException("The expiry date must be in the future.");
        }

        Item item = await _items.FindShareableAsync(ownerId, id, cancellationToken);
        Share share = await _shares.FindByItemAsync(item.Id, cancellationToken)
            ?? throw new ShareNotFoundException($"The item '{id}' is not shared.");
        share.ExpireAt(expiry.Expires);
        await _shares.SaveAsync(cancellationToken);
        return await LinkOfAsync(share, requestOrigin, cancellationToken);
    }

    public async Task StopSharingAsync(string ownerId, string id, CancellationToken cancellationToken)
    {
        Item item = await _items.FindShareableAsync(ownerId, id, cancellationToken);
        if (await _shares.FindByItemAsync(item.Id, cancellationToken) is { } share)
        {
            _shares.Remove(share);
            await _shares.SaveAsync(cancellationToken);
            _audit.ShareLinkRevoked(share.Id, item.Id);
        }
    }

    private async Task<ShareLink> LinkOfAsync(Share share, string requestOrigin, CancellationToken cancellationToken)
    {
        string origin = await _server.PublicOriginAsync(requestOrigin, cancellationToken);
        return new ShareLink(origin + LinkPath + share.Token, share.Expires);
    }
}
