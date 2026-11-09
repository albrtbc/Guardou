using System.Net;
using Guardou.Api.Authentication;
using Guardou.Application.Settings;
using Guardou.Application.Uploads;
using Guardou.Domain.Files;
using Guardou.Domain.Settings;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Features;
using tusdotnet;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Models.Expiration;
using tusdotnet.Stores;

namespace Guardou.Api.Endpoints;

internal static class UploadContentEndpoint
{
    private const string Pattern = "/api/uploads/content";

    private static readonly TusExtensions Extensions = TusExtensions.All.Except(
        TusExtensions.Creation,
        TusExtensions.CreationWithUpload,
        TusExtensions.CreationDeferLength,
        TusExtensions.Concatenation,
        TusExtensions.Termination);

    public static string PathOf(string id)
    {
        return $"{Pattern}/{id}";
    }

    public static void MapUploadContent(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapTus(Pattern, ConfigurationOf).RequireAuthorization();
    }

    private static async Task<DefaultTusConfiguration> ConfigurationOf(HttpContext context)
    {
        ServerSettings settings = await context.RequestServices.GetRequiredService<EffectiveSettings<ServerSettings>>()
            .GetAsync(context.RequestAborted);
        return new DefaultTusConfiguration
        {
            Store = context.RequestServices.GetRequiredService<TusDiskStore>(),
            Expiration = new SlidingExpiration(Upload.Lifetime),
            AllowedExtensions = Extensions,
            MaxAllowedUploadSizeInBytesLong = StoragePolicy.BytesOf(settings.MaxFileSizeMb),
            Events = new Events { OnAuthorizeAsync = AuthorizeAsync, OnBeforeWriteAsync = BeforeWriteAsync },
        };
    }

    private static async Task AuthorizeAsync(AuthorizeContext context)
    {
        if (context.Intent == IntentType.GetOptions)
        {
            return;
        }

        HttpContext http = context.HttpContext;
        if (!await http.RequestServices.GetRequiredService<IAntiforgery>().IsRequestValidAsync(http))
        {
            context.FailRequest(HttpStatusCode.BadRequest, "The antiforgery token is missing or invalid.");
            return;
        }

        UploadSessions sessions = http.RequestServices.GetRequiredService<UploadSessions>();
        if (!await sessions.OwnsAsync(http.User.UserId(), context.FileId, context.CancellationToken))
        {
            context.FailRequest(HttpStatusCode.NotFound);
            return;
        }

        if (context.Intent == IntentType.WriteFile && http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = null;
        }
    }

    private static async Task BeforeWriteAsync(BeforeWriteContext context)
    {
        UploadSessions sessions = context.HttpContext.RequestServices.GetRequiredService<UploadSessions>();
        if (!await sessions.HasRoomToWriteAsync(context.HttpContext.Request.ContentLength ?? 0, context.CancellationToken))
        {
            context.FailRequest(HttpStatusCode.InsufficientStorage, "The server does not have enough free space.");
            return;
        }

        await sessions.RecordWriteAsync(context.FileId, context.CancellationToken);
    }
}
