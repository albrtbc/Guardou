using System.Net;
using System.Net.Http.Json;
using Guardou.Api.Models;

namespace Guardou.IntegrationTests;

public sealed class RateLimitTests(TestServerFactory factory) : IClassFixture<TestServerFactory>
{
    private const string UnknownToken = "AAAAAAAAAAAAAAAAAAAAAA";
    private const int Slack = 50;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RefusesTheEleventhLoginWithinAMinute()
    {
        using HttpClient client = factory.CreateClient();

        for (int attempt = 0; attempt < 10; attempt++)
        {
            HttpResponseMessage allowed = await client.LoginAsync("nobody", "wrong-password");
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        HttpResponseMessage refused = await client.LoginAsync("nobody", "wrong-password");

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.True(refused.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Equal("application/problem+json", refused.Content.Headers.ContentType?.MediaType);
        HttpResponseMessage register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("admin", "Administrator", "admin@example.com", "Integration-pass1"),
            CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, register.StatusCode);
    }

    [Fact]
    public async Task LimitsTheAnonymousShareRoutesButNotTheFileStreamsOrThePreviews()
    {
        await using TestServerFactory own = new();
        using HttpClient client = own.CreateClient();
        string path = $"/api/shares/{UnknownToken}";

        HttpResponseMessage refused = await ExhaustAsync(client, path, 120);
        HttpResponseMessage details = await client.GetAsync($"{path}/items/{UnknownToken}", CancellationToken);
        HttpResponseMessage picture = await client.GetAsync($"{path}/items/{UnknownToken}/creator/picture", CancellationToken);
        HttpResponseMessage preview = await client.GetAsync($"{path}/items/{UnknownToken}/preview?size=thumbnail", CancellationToken);
        HttpResponseMessage view = await client.GetAsync($"{path}/items/{UnknownToken}/view", CancellationToken);
        HttpResponseMessage download = await client.GetAsync($"{path}/items/{UnknownToken}/download", CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.True(refused.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Equal(HttpStatusCode.TooManyRequests, details.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, picture.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, preview.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, view.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/config", CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task LimitsTheSharePreviewsSeparately()
    {
        await using TestServerFactory own = new();
        using HttpClient client = own.CreateClient();
        string path = $"/api/shares/{UnknownToken}/items/{UnknownToken}/preview?size=thumbnail";

        HttpResponseMessage refused = await ExhaustAsync(client, path, 600);
        HttpResponseMessage picture = await client.GetAsync($"/api/shares/{UnknownToken}/items/{UnknownToken}/creator/picture", CancellationToken);
        HttpResponseMessage share = await client.GetAsync($"/api/shares/{UnknownToken}", CancellationToken);
        HttpResponseMessage view = await client.GetAsync($"/api/shares/{UnknownToken}/items/{UnknownToken}/view", CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.True(refused.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Equal(HttpStatusCode.TooManyRequests, picture.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, share.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, view.StatusCode);
    }

    private static async Task<HttpResponseMessage> ExhaustAsync(HttpClient client, string path, int permits)
    {
        // A token bucket refills continuously, so a few extra requests can slip through while the test runs.
        for (int request = 0; request < permits + Slack; request++)
        {
            HttpResponseMessage response = await client.GetAsync(path, CancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                Assert.True(request >= permits, $"The limit was hit after {request} requests, before {permits}.");
                return response;
            }

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        throw new Xunit.Sdk.XunitException($"{path} was never limited.");
    }
}
