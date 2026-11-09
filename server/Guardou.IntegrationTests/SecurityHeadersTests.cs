using System.Net;
using Guardou.Application.Files;

namespace Guardou.IntegrationTests;

public sealed class SecurityHeadersTests : SignedInTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/login")]
    [InlineData("/s/AAAAAAAAAAAAAAAAAAAAAA")]
    public async Task ProtectsTheHtmlPages(string path)
    {
        HttpResponseMessage response = await Factory.CreateClient().GetAsync(path, CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        string policy = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("default-src 'self'", policy, StringComparison.Ordinal);
        Assert.Contains("script-src 'self' 'wasm-unsafe-eval'", policy, StringComparison.Ordinal);
        Assert.Contains("object-src 'none'", policy, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", policy, StringComparison.Ordinal);
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        AssertCommonHeaders(response);
    }

    [Fact]
    public async Task ProtectsTheApiResponses()
    {
        HttpResponseMessage response = await Factory.CreateClient().GetAsync("/api/config", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("default-src 'none'; frame-ancestors 'none'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        AssertCommonHeaders(response);
    }

    [Fact]
    public async Task ProtectsTheErrorResponses()
    {
        HttpResponseMessage response = await Factory.CreateClient().GetAsync("/api/missing", CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        AssertCommonHeaders(response);
    }

    [Fact]
    public async Task LeavesTheApiFileResponsesWithoutAPolicyButNotFrameable()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "notes.txt", "hi");

        HttpResponseMessage response = await Client.GetAsync($"/api/items/{file.Id}/download", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        AssertCommonHeaders(response);
    }

    [Fact]
    public async Task KeepsTheSandboxOnAViewedFileAndAllowsTheViewerToFrameIt()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "page.html", "<p>hi</p>");

        HttpResponseMessage response = await Client.GetAsync($"/api/items/{file.Id}/view", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("sandbox", Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal("SAMEORIGIN", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        AssertCommonHeaders(response);
    }

    [Fact]
    public async Task LetsTheViewerFrameAPdfWithoutAddingAPolicy()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "manual.pdf", "%PDF-1.4");

        HttpResponseMessage response = await Client.GetAsync($"/api/items/{file.Id}/view", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("SAMEORIGIN", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
    }

    private static void AssertCommonHeaders(HttpResponseMessage response)
    {
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("same-origin", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Equal("camera=(), microphone=(), geolocation=()", Assert.Single(response.Headers.GetValues("Permissions-Policy")));
    }
}
