using System.Text.Json;
using System.Text.Json.Serialization;
using Guardou.Api.Authorization;
using Guardou.Api.Configuration;
using Guardou.Api.Endpoints;
using Guardou.Api.Errors;
using Guardou.Api.Filters;
using Guardou.Api.Hosting;
using Guardou.Api.OpenApi;
using Guardou.Application;
using Guardou.Domain;
using Guardou.Infrastructure;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.StaticFiles;

namespace Guardou.Api;

public static class WebServer
{
    private const string AntiforgeryHeaderName = "X-XSRF-TOKEN";
    private const string NoCache = "no-cache";
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);

    public static WebApplication Build(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddFlatEnvironmentVariables();
        AddServices(builder.Services);
        if (DocumentGeneration.IsActive)
        {
            DocumentGeneration.RemoveBackgroundServices(builder.Services);
        }

        WebApplication app = builder.Build();
        UsePipeline(app);
        return app;
    }

    private static void AddServices(IServiceCollection services)
    {
        services.AddHostedService<StartupInitializer>();
        services.AddApplication();
        services.AddInfrastructure();
        services.AddWebInfrastructure();

        services.AddControllers(options =>
        {
            options.Filters.Add<AntiforgeryValidationFilter>();
            options.OutputFormatters.RemoveType<StringOutputFormatter>();
        })
            .AddApplicationPart(typeof(WebServer).Assembly)
            .AddJsonOptions(options => ConfigureJson(options.JsonSerializerOptions));
        services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));
        Policies.Add(services.AddAuthorizationBuilder());
        services.AddAntiforgery(options =>
        {
            options.HeaderName = AntiforgeryHeaderName;
            options.Cookie.Name = Product.CookieName("antiforgery");
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });
        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IConfiguration>((options, configuration) => TrustedProxies.Apply(options, configuration[TrustedProxies.Key]));
        RateLimits.Add(services);
        services.AddProblemDetails();
        services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
        services.AddOpenApi(options =>
        {
            options.AddSchemaTransformer<StringEnumSchemaTransformer>();
            options.AddDocumentTransformer<FormFileDocumentTransformer>();
        });
        services.Configure<HostOptions>(options => options.ShutdownTimeout = ShutdownTimeout);
        services.AddHostedService<TrashPurgeWorker>();
        services.AddHostedService<TaskWorkerPool>();
        services.AddHostedService<UploadCleanupWorker>();
    }

    private static void RevalidateStaticFile(StaticFileResponseContext context)
    {
        context.Context.Response.Headers.CacheControl = NoCache;
    }

    private static void ConfigureJson(JsonSerializerOptions options)
    {
        options.NumberHandling = JsonNumberHandling.Strict;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    private static void UsePipeline(WebApplication app)
    {
        app.UseForwardedHeaders();
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseSecurityHeaders();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseDefaultFiles();
        StaticFileOptions staticFiles = new() { OnPrepareResponse = RevalidateStaticFile };
        app.UseStaticFiles(staticFiles);
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapUploadContent();
        app.MapHealthChecks("/health").AllowAnonymous();
        app.MapFallback("/api/{**path}", () => Results.NotFound()).AllowAnonymous();
        app.MapFallbackToFile("index.html", staticFiles).AllowAnonymous();
    }
}
