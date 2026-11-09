using System.Net.Mime;
using Guardou.Application.Files;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Guardou.Api.ActionResults;

internal sealed class ArchiveResult(ItemArchive archive, ItemDownloads downloads, ArchiveAudience audience) : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext context)
    {
        HttpResponse response = context.HttpContext.Response;
        using IDisposable slot = downloads.AcquireArchiveSlot(audience);
        AllowZipHeaderWrites(context.HttpContext);
        ContentDispositionHeaderValue disposition = new(DispositionTypeNames.Attachment);
        disposition.SetHttpFileName(archive.Name);
        response.ContentType = MediaTypeNames.Application.Zip;
        response.Headers.ContentDisposition = disposition.ToString();
        await downloads.WriteArchiveAsync(archive, response.Body, context.HttpContext.RequestAborted);
    }

    private static void AllowZipHeaderWrites(HttpContext context)
    {
        if (context.Features.Get<IHttpBodyControlFeature>() is { } bodyControl)
        {
            bodyControl.AllowSynchronousIO = true;
        }
    }
}
