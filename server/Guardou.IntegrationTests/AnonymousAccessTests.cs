using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Guardou.IntegrationTests;

public sealed class AnonymousAccessTests(TestServerFactory factory) : IClassFixture<TestServerFactory>
{
    private const string Token = "AAAAAAAAAAAAAAAAAAAAAA";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RequiresASessionByDefault()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();

        AuthorizationPolicy? fallback = await scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>().GetFallbackPolicyAsync();

        Assert.NotNull(fallback);
        Assert.Contains(fallback.Requirements, requirement => requirement is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);
    }

    [Theory]
    [InlineData("/", HttpStatusCode.OK)]
    [InlineData("/login", HttpStatusCode.OK)]
    [InlineData("/health", HttpStatusCode.OK)]
    [InlineData("/api/config", HttpStatusCode.OK)]
    [InlineData("/api/auth/antiforgery", HttpStatusCode.OK)]
    [InlineData("/api/missing", HttpStatusCode.NotFound)]
    [InlineData($"/api/shares/{Token}", HttpStatusCode.NotFound)]
    [InlineData($"/api/shares/{Token}/items/{Token}", HttpStatusCode.NotFound)]
    [InlineData($"/api/shares/{Token}/items/{Token}/view", HttpStatusCode.NotFound)]
    [InlineData($"/api/shares/{Token}/items/{Token}/download", HttpStatusCode.NotFound)]
    [InlineData($"/api/shares/{Token}/items/{Token}/preview?size=thumbnail", HttpStatusCode.NotFound)]
    [InlineData($"/api/shares/{Token}/items/{Token}/creator/picture", HttpStatusCode.NotFound)]
    [InlineData("/api/auth/oidc/login", HttpStatusCode.Redirect)]
    [InlineData("/api/auth/oidc/complete", HttpStatusCode.Redirect)]
    public async Task AnswersAnonymousRoutes(string path, HttpStatusCode expected)
    {
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        HttpResponseMessage response = await client.GetAsync(path, CancellationToken);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/auth/me")]
    [InlineData("/api/items/root")]
    [InlineData("/api/users/me/preferences")]
    [InlineData("/api/uploads/content/abc")]
    [InlineData("/api/settings/server")]
    public async Task RefusesAuthenticatedRoutesWithoutASession(string path)
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync(path, CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RefusesToJoinAShareWithoutASession()
    {
        using HttpClient client = factory.CreateClient();
        await client.RefreshAntiforgeryTokenAsync();

        HttpResponseMessage response = await client.PostAsync($"/api/shares/{Token}/join", content: null, CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
