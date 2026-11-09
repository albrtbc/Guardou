using System.Net.Mime;
using Guardou.Api.Authentication;
using Guardou.Api.Endpoints;
using Guardou.Api.Models;
using Guardou.Application.Files;
using Guardou.Application.Uploads;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Guardou.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/uploads")]
public sealed class UploadsController(UploadSessions sessions, UploadCompleter completer) : ControllerBase
{
    [HttpPost(Name = "StartUpload")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<UploadSession>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status507InsufficientStorage)]
    public async Task<IActionResult> Start(StartUploadRequest request, CancellationToken cancellationToken)
    {
        UploadRequest upload = new(request.FolderId, request.FileName, request.RelativePath, request.Size, request.Modified, request.Conflict);
        UploadSession? session = await sessions.StartAsync(User.UserId(), upload, cancellationToken);
        return session is null ? NoContent() : Created(UploadContentEndpoint.PathOf(session.Id), session);
    }

    [HttpPost("{id}/complete", Name = "CompleteUpload")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<ItemEntry>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete(string id, CompleteUploadRequest request, CancellationToken cancellationToken)
    {
        ItemEntry? entry = await completer.CompleteAsync(User.UserId(), id, request.Conflict, cancellationToken);
        return entry is null ? NoContent() : CreatedAtRoute("GetItem", new { id = entry.Id }, entry);
    }

    [HttpDelete("{id}", Name = "CancelUpload")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(string id, CancellationToken cancellationToken)
    {
        await sessions.CancelAsync(User.UserId(), id, cancellationToken);
        return NoContent();
    }
}
