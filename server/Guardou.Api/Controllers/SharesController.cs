using Guardou.Api.ActionResults;
using Guardou.Api.Authentication;
using Guardou.Api.Hosting;
using Guardou.Application.Files;
using Guardou.Application.Shares;
using Guardou.Domain.Previews;
using Guardou.Infrastructure.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Guardou.Api.Controllers;

[ApiController]
[Authorize]
public sealed class SharesController(ItemSharing sharing, SharedItems shared, DirectSharing directSharing, ItemDownloads downloads)
    : ControllerBase
{
    private const string ItemShareRoute = "api/items/{id}/share";
    private const string SharedItemRoute = "api/shares/{token}/items/{id}";

    [HttpGet(ItemShareRoute, Name = "GetItemShare")]
    [ProducesResponseType<ShareLink>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ShareLink> GetItemShare(string id, CancellationToken cancellationToken)
    {
        return sharing.GetAsync(User.UserId(), id, Request.Origin(), cancellationToken);
    }

    [HttpPut(ItemShareRoute, Name = "ShareItem")]
    [ProducesResponseType<ShareLink>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ShareLink> ShareItem(string id, CancellationToken cancellationToken)
    {
        return sharing.ShareAsync(User.UserId(), id, Request.Origin(), cancellationToken);
    }

    [HttpPut(ItemShareRoute + "/expiry", Name = "ChangeItemShareExpiry")]
    [ProducesResponseType<ShareLink>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ShareLink> ChangeItemShareExpiry(string id, ShareExpiry expiry, CancellationToken cancellationToken)
    {
        return sharing.ChangeExpiryAsync(User.UserId(), id, expiry, Request.Origin(), cancellationToken);
    }

    [HttpDelete(ItemShareRoute, Name = "StopSharingItem")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StopSharingItem(string id, CancellationToken cancellationToken)
    {
        await sharing.StopSharingAsync(User.UserId(), id, cancellationToken);
        return NoContent();
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Shares)]
    [HttpGet("api/shares/{token}", Name = "GetSharedItem")]
    [ProducesResponseType<ItemEntry>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public Task<ItemEntry> GetSharedItem(string token, CancellationToken cancellationToken)
    {
        return shared.GetAsync(token, cancellationToken);
    }

    [HttpPost("api/shares/{token}/join", Name = "JoinSharedItem")]
    [ProducesResponseType<ItemEntry>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ItemEntry> JoinSharedItem(string token, CancellationToken cancellationToken)
    {
        return directSharing.JoinLinkAsync(User.UserId(), token, cancellationToken);
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Shares)]
    [HttpGet(SharedItemRoute, Name = "GetSharedItemDetails")]
    [ProducesResponseType<ItemDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public Task<ItemDetails> GetSharedItemDetails(string token, string id, CancellationToken cancellationToken)
    {
        return shared.GetDetailsAsync(token, id, cancellationToken);
    }

    [AllowAnonymous]
    [HttpGet(SharedItemRoute + "/download", Name = "DownloadSharedItem")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status206PartialContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DownloadSharedItem(string token, string id, CancellationToken cancellationToken)
    {
        FileDownload? file = await shared.TryOpenFileAsync(token, id, cancellationToken);
        if (file is null)
        {
            ItemArchive archive = await shared.PrepareArchiveAsync(token, id, cancellationToken);
            Response.Headers.CacheControl = FileResponses.NoStorePolicy;
            return new ArchiveResult(archive, downloads, ArchiveAudience.Anonymous);
        }

        return this.Download(file, FileResponses.NoStorePolicy);
    }

    [AllowAnonymous]
    [HttpGet(SharedItemRoute + "/view", Name = "ViewSharedItem")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status206PartialContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ViewSharedItem(string token, string id, CancellationToken cancellationToken)
    {
        return this.View(await shared.OpenFileAsync(token, id, cancellationToken), FileResponses.NoStorePolicy);
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.SharePreviews)]
    [HttpGet(SharedItemRoute + "/creator/picture", Name = "GetSharedItemCreatorPicture")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetSharedItemCreatorPicture(string token, string id, CancellationToken cancellationToken)
    {
        return this.Picture(await shared.OpenCreatorPictureAsync(token, id, cancellationToken));
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.SharePreviews)]
    [HttpGet(SharedItemRoute + "/preview", Name = "GetSharedItemPreview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetSharedItemPreview(
        string token,
        string id,
        [FromQuery] PreviewSize size,
        CancellationToken cancellationToken)
    {
        return this.Preview(await shared.OpenPreviewAsync(token, id, size, cancellationToken), FileResponses.SharedPreviewCachePolicy);
    }
}
