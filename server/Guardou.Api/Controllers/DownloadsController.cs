using Guardou.Api.Authentication;
using Guardou.Api.ActionResults;
using Guardou.Application.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Guardou.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/items")]
public sealed class DownloadsController(ItemDownloads downloads) : ControllerBase
{
    [HttpGet("{id}/download", Name = "DownloadItem")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status206PartialContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Download(string id, CancellationToken cancellationToken)
    {
        FileDownload? file = await downloads.TryOpenFileAsync(User.UserId(), id, cancellationToken);
        if (file is null)
        {
            return await ArchiveAsync([id], cancellationToken);
        }

        return this.Download(file);
    }

    [HttpGet("{id}/view", Name = "ViewItem")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status206PartialContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> View(string id, CancellationToken cancellationToken)
    {
        return this.View(await downloads.OpenFileAsync(User.UserId(), id, cancellationToken));
    }

    [HttpGet("archive", Name = "DownloadArchive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> Archive([FromQuery] IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        return ArchiveAsync(ids, cancellationToken);
    }

    private async Task<IActionResult> ArchiveAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ItemArchive archive = await downloads.PrepareArchiveAsync(User.UserId(), ids, cancellationToken);
        return new ArchiveResult(archive, downloads, ArchiveAudience.SignedIn);
    }
}
