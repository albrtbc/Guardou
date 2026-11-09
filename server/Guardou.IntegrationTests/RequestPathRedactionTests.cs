using Guardou.Api.Errors;
using Guardou.Application.Quotas;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Guardou.IntegrationTests;

public sealed class RequestPathRedactionTests
{
    private const string Token = "AbCdEfGhIjKlMnOpQrStUv";

    [Theory]
    [InlineData("/api/shares/AbCdEfGhIjKlMnOpQrStUv", "/api/shares/***")]
    [InlineData("/api/shares/AbCdEfGhIjKlMnOpQrStUv/items/xyz/download", "/api/shares/***/items/xyz/download")]
    [InlineData("/API/Shares/AbCdEfGhIjKlMnOpQrStUv/join", "/API/Shares/***/join")]
    [InlineData("/api/items/abc/share", "/api/items/abc/share")]
    [InlineData("/api/shares", "/api/shares")]
    [InlineData("/s/AbCdEfGhIjKlMnOpQrStUv", "/s/***")]
    [InlineData("/S/AbCdEfGhIjKlMnOpQrStUv/extra", "/S/***/extra")]
    [InlineData("/s/", "/s/")]
    [InlineData("/settings/abc", "/settings/abc")]
    [InlineData("", "")]
    public void RedactsTheShareTokenAndKeepsTheRestOfThePath(string path, string expected)
    {
        Assert.Equal(expected, RequestPathRedaction.Redact(new PathString(path)));
    }

    [Fact]
    public async Task LogsAServerErrorWithoutTheShareToken()
    {
        (ProblemDetailsExceptionHandler handler, CapturingLoggerProvider logs) = CreateHandler();
        DefaultHttpContext context = ContextOf("/api/shares/" + Token + "/items/xyz/download");

        await handler.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        CapturedLog log = Assert.Single(logs.Logs);
        Assert.Equal(LogLevel.Error, log.Level);
        Assert.Equal("/api/shares/***/items/xyz/download", log.Properties["Path"]);
        Assert.DoesNotContain(log.Texts, text => text.Contains(Token, StringComparison.Ordinal));
        Assert.Contains("/api/shares/***/items/xyz/download", log.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LogsAFullDiskRefusalWithoutTheShareToken()
    {
        (ProblemDetailsExceptionHandler handler, CapturingLoggerProvider logs) = CreateHandler();
        DefaultHttpContext context = ContextOf("/api/shares/" + Token + "/items/xyz/download");

        await handler.TryHandleAsync(context, new InsufficientStorageException(), CancellationToken.None);

        CapturedLog log = Assert.Single(logs.Logs);
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.DoesNotContain(log.Texts, text => text.Contains(Token, StringComparison.Ordinal));
    }

    private static DefaultHttpContext ContextOf(string path)
    {
        DefaultHttpContext context = new();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        return context;
    }

    private static (ProblemDetailsExceptionHandler Handler, CapturingLoggerProvider Logs) CreateHandler()
    {
        CapturingLoggerProvider logs = new();
        ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        return (new ProblemDetailsExceptionHandler(new CompletedProblemDetailsService(), factory.CreateLogger<ProblemDetailsExceptionHandler>()), logs);
    }

    private sealed class CompletedProblemDetailsService : IProblemDetailsService
    {
        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            return ValueTask.FromResult(true);
        }
    }
}
