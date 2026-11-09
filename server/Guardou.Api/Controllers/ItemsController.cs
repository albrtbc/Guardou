using System.Net.Mime;
using Guardou.Api.Authentication;
using Guardou.Api.Models;
using Guardou.Application.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Guardou.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/items")]
public sealed class ItemsController(ItemBrowser browser, ItemOrganizer organizer, TrashService trash)
    : ControllerBase
{
    [HttpGet("{id}", Name = "GetItem")]
    [ProducesResponseType<ItemDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ItemDetails> Get(
        string id,
        [FromQuery] bool content,
        CancellationToken cancellationToken)
    {
        return browser.GetAsync(User.UserId(), new ItemDetailsRequest(id, content), cancellationToken);
    }

    [HttpGet("{id}/children", Name = "GetItemChildren")]
    [ProducesResponseType<IReadOnlyList<ItemEntry>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<ItemEntry>> GetChildren(string id, [FromQuery] ItemFilter? only, CancellationToken cancellationToken)
    {
        return browser.GetChildrenAsync(User.UserId(), id, only, cancellationToken);
    }

    [HttpGet("usage", Name = "GetStorageUsage")]
    [ProducesResponseType<StorageUsage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<StorageUsage> GetUsage(CancellationToken cancellationToken)
    {
        return browser.GetUsageAsync(User.UserId(), cancellationToken);
    }

    [HttpPost("{id}/folders", Name = "CreateFolder")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<ItemEntry>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateFolder(string id, NameRequest request, CancellationToken cancellationToken)
    {
        ItemEntry folder = await organizer.CreateFolderAsync(User.UserId(), id, request.Name, cancellationToken);
        return CreatedAtRoute("GetItem", new { id = folder.Id }, folder);
    }

    [HttpPatch("{id}", Name = "RenameItem")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<ItemEntry>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ItemEntry> Rename(string id, NameRequest request, CancellationToken cancellationToken)
    {
        return organizer.RenameAsync(User.UserId(), id, request.Name, cancellationToken);
    }

    [HttpPost("{id}/move", Name = "MoveItem")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<ItemEntry>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Move(string id, TransferRequest request, CancellationToken cancellationToken)
    {
        ItemEntry? item = await organizer.MoveAsync(User.UserId(), id, request.DestinationId, request.Conflict, cancellationToken);
        return item is null ? NoContent() : Ok(item);
    }

    [HttpPost("{id}/copy", Name = "CopyItem")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<ItemEntry>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status507InsufficientStorage)]
    public async Task<IActionResult> Copy(string id, TransferRequest request, CancellationToken cancellationToken)
    {
        ItemEntry? copy = await organizer.CopyAsync(User.UserId(), id, request.DestinationId, request.Conflict, cancellationToken);
        return copy is null ? NoContent() : CreatedAtRoute("GetItem", new { id = copy.Id }, copy);
    }

    [HttpPost("trash", Name = "MoveItemsToTrash")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MoveToTrash(ItemSelection selection, CancellationToken cancellationToken)
    {
        await trash.MoveToTrashAsync(User.UserId(), selection.Ids, cancellationToken);
        return NoContent();
    }
}
