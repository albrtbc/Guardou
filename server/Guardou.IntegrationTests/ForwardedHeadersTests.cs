using System.Net;
using System.Net.Http.Json;
using Guardou.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Guardou.IntegrationTests;

public sealed class ForwardedHeadersTests
{
    private const string TrustedProxiesKey = "Hosting:TrustedProxies";
    private const string RemoteHeader = RemoteAddressFilter.Header;
    private const int LoginBudget = 10;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("10.1.2.3", "10.0.0.0/8")]
    [InlineData("192.168.1.5", "172.16.0.1, 192.168.1.5")]
    public async Task MarksTheCookiesSecureBehindATrustedProxy(string remote, string trusted)
    {
        await using TestServerFactory factory = CreateFactory(trusted);

        IReadOnlyList<string> cookies = await LoginCookiesAsync(factory, remote);

        Assert.NotEmpty(cookies);
        Assert.All(cookies, cookie => Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task IgnoresTheForwardedProtocolFromAnUntrustedSource()
    {
        await using TestServerFactory factory = CreateFactory("10.0.0.0/8");

        IReadOnlyList<string> cookies = await LoginCookiesAsync(factory, "203.0.113.9");

        Assert.NotEmpty(cookies);
        Assert.All(cookies, cookie => Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TrustsNoProxyByDefault()
    {
        await using TestServerFactory factory = CreateFactory(trusted: null);

        IReadOnlyList<string> cookies = await LoginCookiesAsync(factory, "10.1.2.3");

        Assert.NotEmpty(cookies);
        Assert.All(cookies, cookie => Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task KeepsTheCookiesPlainOverPlainHttp()
    {
        await using TestServerFactory factory = CreateFactory("10.0.0.0/8");

        IReadOnlyList<string> cookies = await LoginCookiesAsync(factory, "10.1.2.3", forwardedProtocol: "http");

        Assert.NotEmpty(cookies);
        Assert.All(cookies, cookie => Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("not-an-address")]
    [InlineData("10.1")]
    [InlineData("172.18")]
    [InlineData("10")]
    [InlineData("10.1/16")]
    [InlineData("10.0.0.0/33")]
    [InlineData("10.0.0.5, 172.18")]
    public async Task RejectsAnInvalidProxyList(string list)
    {
        await using TestServerFactory factory = CreateFactory(list);

        Exception exception = await Assert.ThrowsAnyAsync<Exception>(() => factory.CreateClient().GetAsync("/api/config", CancellationToken));

        Assert.Contains("TRUSTED_PROXIES", MessagesOf(exception), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("172.18.0.2")]
    [InlineData("172.18.0.0/16")]
    [InlineData("::1, 2001:db8::/32, 10.0.0.5")]
    [InlineData("")]
    public async Task AcceptsAValidProxyList(string list)
    {
        await using TestServerFactory factory = CreateFactory(list);

        HttpResponseMessage response = await factory.CreateClient().GetAsync("/api/config", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GivesEachForwardedClientItsOwnLoginBudgetBehindATrustedProxy()
    {
        await using TestServerFactory factory = CreateFactory("10.0.0.0/8");
        using HttpClient first = ClientOf(factory, "10.1.2.3", forwardedFor: "203.0.113.1");
        using HttpClient second = ClientOf(factory, "10.1.2.3", forwardedFor: "203.0.113.2");

        await ExhaustLoginBudgetAsync(first);

        Assert.Equal(HttpStatusCode.TooManyRequests, await AttemptLoginAsync(first));
        Assert.Equal(HttpStatusCode.Unauthorized, await AttemptLoginAsync(second));
    }

    [Fact]
    public async Task IgnoresTheForwardedClientFromAnUntrustedSource()
    {
        await using TestServerFactory factory = CreateFactory("10.0.0.0/8");
        using HttpClient first = ClientOf(factory, "203.0.113.9", forwardedFor: "198.51.100.1");
        using HttpClient second = ClientOf(factory, "203.0.113.9", forwardedFor: "198.51.100.2");

        await ExhaustLoginBudgetAsync(first);

        Assert.Equal(HttpStatusCode.TooManyRequests, await AttemptLoginAsync(second));
    }

    [Fact]
    public async Task SharesTheLoginBudgetAcrossAnIpv6Prefix()
    {
        await using TestServerFactory factory = CreateFactory(trusted: null);
        using HttpClient first = ClientOf(factory, "2001:db8:1:2::1");
        using HttpClient sameSubnet = ClientOf(factory, "2001:db8:1:2:ffff::7");
        using HttpClient otherSubnet = ClientOf(factory, "2001:db8:1:3::1");

        await ExhaustLoginBudgetAsync(first);

        Assert.Equal(HttpStatusCode.TooManyRequests, await AttemptLoginAsync(sameSubnet));
        Assert.Equal(HttpStatusCode.Unauthorized, await AttemptLoginAsync(otherSubnet));
    }

    [Fact]
    public async Task CountsAnIpv4MappedClientAsTheIpv4Address()
    {
        await using TestServerFactory factory = CreateFactory(trusted: null);
        using HttpClient plain = ClientOf(factory, "192.0.2.7");
        using HttpClient mapped = ClientOf(factory, "::ffff:192.0.2.7");

        await ExhaustLoginBudgetAsync(plain);

        Assert.Equal(HttpStatusCode.TooManyRequests, await AttemptLoginAsync(mapped));
    }

    private static HttpClient ClientOf(TestServerFactory factory, string remote, string? forwardedFor = null)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(RemoteHeader, remote);
        if (forwardedFor is not null)
        {
            client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        }

        return client;
    }

    private static async Task ExhaustLoginBudgetAsync(HttpClient client)
    {
        for (int attempt = 0; attempt < LoginBudget; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, await AttemptLoginAsync(client));
        }
    }

    private static async Task<HttpStatusCode> AttemptLoginAsync(HttpClient client)
    {
        return (await client.LoginAsync("nobody", "wrong-password")).StatusCode;
    }

    private static string MessagesOf(Exception exception)
    {
        List<string> messages = [];
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }

        return string.Join(" | ", messages);
    }

    private static TestServerFactory CreateFactory(string? trusted)
    {
        return new TestServerFactory
        {
            Settings = trusted is null ? new Dictionary<string, string>() : new Dictionary<string, string> { [TrustedProxiesKey] = trusted },
            TestServices = services => services.AddSingleton<IStartupFilter, RemoteAddressFilter>(),
        };
    }

    private static async Task<IReadOnlyList<string>> LoginCookiesAsync(TestServerFactory factory, string remote, string forwardedProtocol = "https")
    {
        await factory.EnsureAdministratorAsync();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add(RemoteHeader, remote);
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", forwardedProtocol);

        HttpResponseMessage antiforgery = await client.GetAsync("/api/auth/antiforgery", CancellationToken);
        AntiforgeryToken? token = await antiforgery.Content.ReadFromJsonAsync<AntiforgeryToken>(CancellationToken);
        string antiforgeryCookie = antiforgery.Headers.GetValues("Set-Cookie").Single();
        using HttpRequestMessage login = new(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(TestServerFactory.AdminUsername, TestServerFactory.AdminPassword, RememberMe: false)),
        };
        login.Headers.Add("Cookie", antiforgeryCookie.Split(';')[0]);
        login.Headers.Add("X-XSRF-TOKEN", token?.Token);
        HttpResponseMessage response = await client.SendAsync(login, CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return [antiforgeryCookie, .. response.Headers.GetValues("Set-Cookie")];
    }
}
