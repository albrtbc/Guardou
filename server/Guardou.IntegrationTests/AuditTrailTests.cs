using System.Net;
using System.Net.Http.Json;
using Guardou.Api.Models;
using Guardou.Application.Audit;
using Guardou.Application.Files;
using Guardou.Application.Shares;
using Guardou.Application.Users;
using Guardou.Domain.Authentication;
using Guardou.Domain.Settings;
using Guardou.Domain.Shares;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Guardou.IntegrationTests;

public sealed class AuditTrailTests : SignedInTests
{
    private const string TrustedProxiesKey = "Hosting:TrustedProxies";
    private const string WrongPassword = "Wrong-Pass-Never-Logged-1";
    private const string ForwardedClient = "203.0.113.50";

    private readonly CapturingLoggerProvider _logs = new();

    private IEnumerable<CapturedLog> Audit => _logs.Logs.Where(log => log.Category == AuditLog.Category);

    private IEnumerable<CapturedLog> AuditEvents(int firstId, int lastId)
    {
        return Audit.Where(log => log.EventId.Id >= firstId && log.EventId.Id <= lastId);
    }

    [Fact]
    public async Task LogsAFailedSignInWithTheUsernameAndNeverThePassword()
    {
        using HttpClient client = ClientBehindProxy();
        _logs.Clear();

        HttpResponseMessage response = await client.LoginAsync(TestServerFactory.AdminUsername, WrongPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        CapturedLog log = Assert.Single(Audit);
        Assert.Equal(9002, log.EventId.Id);
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.Equal(TestServerFactory.AdminUsername, log.Properties["Username"]);
        Assert.Equal(ForwardedClient, log.Properties["ClientIp"]);
        Assert.DoesNotContain(_logs.Logs.SelectMany(entry => entry.Texts), text => text.Contains(WrongPassword, StringComparison.Ordinal));
    }

    [Fact]
    public async Task LogsAnUnknownUsernameAsAFailedSignIn()
    {
        _logs.Clear();

        await Factory.CreateClient().LoginAsync("nobody", WrongPassword);

        CapturedLog log = Assert.Single(Audit);
        Assert.Equal(9002, log.EventId.Id);
        Assert.Equal("nobody", log.Properties["Username"]);
    }

    [Fact]
    public async Task LogsALockoutWithItsOwnEventAndTheRefusalsThatFollow()
    {
        using HttpClient client = Factory.CreateClient();
        _logs.Clear();

        for (int attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.LoginAsync(TestServerFactory.AdminUsername, WrongPassword)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.LoginAsync(TestServerFactory.AdminUsername, TestServerFactory.AdminPassword)).StatusCode);

        Assert.Equal([9002, 9002, 9002, 9002, 9003, 9004], Audit.Select(log => log.EventId.Id));
        Assert.All(Audit, log => Assert.Equal(LogLevel.Warning, log.Level));
        Assert.DoesNotContain(_logs.Logs.SelectMany(entry => entry.Texts), text => text.Contains(TestServerFactory.AdminPassword, StringComparison.Ordinal));
    }

    [Fact]
    public async Task LogsASuccessfulSignInAndASignOutWithTheUserAndTheAddress()
    {
        using HttpClient client = ClientBehindProxy();
        string adminId = (await Client.FindUserAsync(TestServerFactory.AdminUsername)).Id;
        _logs.Clear();

        await client.LoginAsync(TestServerFactory.AdminUsername, TestServerFactory.AdminPassword);
        await client.PostAsync("/api/auth/logout", null, CancellationToken);

        Assert.Equal([9001, 9005], Audit.Select(log => log.EventId.Id));
        CapturedLog signIn = Audit.First();
        CapturedLog signOut = Audit.Last();
        Assert.Equal(adminId, signIn.Properties["UserId"]);
        Assert.Equal(ForwardedClient, signIn.Properties["ClientIp"]);
        Assert.Equal(adminId, signOut.Properties["ActorId"]);
        Assert.Equal(ForwardedClient, signOut.Properties["ClientIp"]);
    }

    [Fact]
    public async Task LogsTheRegistrationOfTheFirstAdministrator()
    {
        await using TestServerFactory own = FactoryWith(trustedProxies: null);
        using HttpClient client = own.CreateClient();
        _logs.Clear();

        await client.RegisterAsync(TestServerFactory.AdminUsername, TestServerFactory.AdminDisplayName, TestServerFactory.AdminEmail, TestServerFactory.AdminPassword);

        Assert.Equal([9006, 9001], Audit.Select(log => log.EventId.Id));
        Assert.DoesNotContain(_logs.Logs.SelectMany(entry => entry.Texts), text => text.Contains(TestServerFactory.AdminPassword, StringComparison.Ordinal));
    }

    [Fact]
    public async Task LogsTheUserAdministrationWithTheActorAndNeverThePassword()
    {
        string adminId = (await Client.FindUserAsync(TestServerFactory.AdminUsername)).Id;
        const string password = "Carol-Secret-Pass-1";
        _logs.Clear();

        UserAccount carol = await UsersApi.ReadAccountAsync(
            await Client.CreateUserAsync(new UserRequest("carol", "Carol", "carol@example.com", password, IsAdmin: false)));
        await Client.UpdateUserAsync(carol.Id, new UserRequest("carol", "Carol", "carol@example.com", Password: null, IsAdmin: true));
        await Client.UpdateUserAsync(carol.Id, new UserRequest("carol", "Carol C.", "carol@example.com", password + "2", IsAdmin: true));
        await Client.DeleteAsync($"/api/users/{carol.Id}/logins/{OidcSettings.Provider}", CancellationToken);
        await LinkLoginAsync(carol.Id);
        await Client.DeleteAsync($"/api/users/{carol.Id}/logins/{OidcSettings.Provider}", CancellationToken);
        await Client.DeleteAsync($"/api/users/{carol.Id}/logins/{OidcSettings.Provider}", CancellationToken);
        await Client.DeleteUserAsync(carol.Id);

        Assert.Equal([9010, 9011, 9013, 9011, 9014, 9012], AuditEvents(9010, 9019).Select(log => log.EventId.Id));
        Assert.All(AuditEvents(9010, 9019), log => Assert.Equal(adminId, log.Properties["ActorId"]));
        Assert.All(AuditEvents(9010, 9019), log => Assert.Equal(carol.Id, log.Properties["UserId"]));
        Assert.Equal(true, AuditEvents(9013, 9013).Single().Properties["IsAdmin"]);
        Assert.Equal([false, true], AuditEvents(9011, 9011).Select(log => (bool)log.Properties["PasswordChanged"]!));
        Assert.DoesNotContain(_logs.Logs.SelectMany(entry => entry.Texts), text => text.Contains("Carol-Secret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LogsASettingsChangeWithTheSectionAndNotTheValues()
    {
        string adminId = (await Client.FindUserAsync(TestServerFactory.AdminUsername)).Id;
        _logs.Clear();

        HttpResponseMessage response = await Client.PutAsJsonAsync("/api/settings/server", new ServerSettings("https://private-host.example.com"), CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        CapturedLog log = Assert.Single(Audit);
        Assert.Equal(9020, log.EventId.Id);
        Assert.Equal("server", log.Properties["Section"]);
        Assert.Equal(adminId, log.Properties["ActorId"]);
        Assert.DoesNotContain(log.Texts, text => text.Contains("private-host", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LogsTheCreationAndTheRevocationOfAShareLinkOnce()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "report.txt", "a");
        string adminId = (await Client.FindUserAsync(TestServerFactory.AdminUsername)).Id;
        _logs.Clear();

        string token = await ShareAsync(file.Id);
        await ShareAsync(file.Id);
        HttpResponseMessage revoked = await Client.DeleteAsync($"/api/items/{file.Id}/share", CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.Equal([9030, 9031], Audit.Select(log => log.EventId.Id));
        Assert.All(Audit, log => Assert.Equal(adminId, log.Properties["ActorId"]));
        Assert.All(Audit, log => Assert.Equal(file.Id, log.Properties["ItemId"]));
        Assert.Equal(Audit.First().Properties["ShareId"], Audit.Last().Properties["ShareId"]);
        Assert.DoesNotContain(_logs.Logs.SelectMany(entry => entry.Texts), text => text.Contains(token, StringComparison.Ordinal));
    }

    [Fact]
    public async Task LogsDirectSharesAddedAndRemoved()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "report.txt", "a");
        string bobId = (await Client.FindUserAsync(BobUsername)).Id;
        _logs.Clear();

        await Client.PutAsync($"/api/items/{file.Id}/recipients/{bobId}", null, CancellationToken);
        await Client.PutAsync($"/api/items/{file.Id}/recipients/{bobId}", null, CancellationToken);
        await Client.DeleteAsync($"/api/items/{file.Id}/recipients/{bobId}", CancellationToken);

        Assert.Equal([9032, 9033], Audit.Select(log => log.EventId.Id));
        Assert.All(Audit, log => Assert.Equal(file.Id, log.Properties["ItemId"]));
        Assert.All(Audit, log => Assert.Equal(bobId, log.Properties["RecipientId"]));
    }

    [Fact]
    public async Task LogsAnonymousShareAccessWithTheShareTheItemTheActionAndTheAddressButNotTheToken()
    {
        using HttpClient clientBehindProxy = ClientBehindProxy();
        ItemEntry docs = await Client.CreateFolderEntryAsync(ItemsApi.Root, "docs");
        ItemEntry file = await Client.UploadEntryAsync(docs.Id, "a.txt", "a");
        string token = await ShareAsync(docs.Id);
        Guid shareId = await ShareIdAsync(token);
        _logs.Clear();

        await clientBehindProxy.GetAsync($"/api/shares/{token}", CancellationToken);
        await clientBehindProxy.GetAsync($"/api/shares/{token}/items/{docs.Id}", CancellationToken);
        await clientBehindProxy.GetAsync($"/api/shares/{token}/items/{file.Id}/download", CancellationToken);
        await clientBehindProxy.GetAsync($"/api/shares/{token}/items/{file.Id}/view", CancellationToken);
        await clientBehindProxy.GetAsync($"/api/shares/{token}/items/{docs.Id}/download", CancellationToken);

        Assert.All(AuditEvents(9040, 9041), log => Assert.Equal(9040, log.EventId.Id));
        Assert.Equal(
            [ShareAccessKind.Open, ShareAccessKind.Details, ShareAccessKind.Download, ShareAccessKind.View, ShareAccessKind.Archive],
            AuditEvents(9040, 9040).Select(log => (ShareAccessKind)log.Properties["Action"]!));
        Assert.Equal(
            [docs.Id, docs.Id, file.Id, file.Id, docs.Id],
            AuditEvents(9040, 9040).Select(log => (string)log.Properties["ItemId"]!));
        Assert.All(AuditEvents(9040, 9040), log => Assert.Equal(shareId, log.Properties["ShareId"]));
        Assert.All(AuditEvents(9040, 9040), log => Assert.Equal(ForwardedClient, log.Properties["ClientIp"]));
        Assert.All(AuditEvents(9040, 9040), log => Assert.Null(log.Properties["ActorId"]));
        Assert.DoesNotContain(_logs.Logs.SelectMany(entry => entry.Texts), text => text.Contains(token, StringComparison.Ordinal));
    }

    [Fact]
    public async Task LogsTheSignedInUserWhoOpensAShareLink()
    {
        ItemEntry file = await Client.UploadEntryAsync(ItemsApi.Root, "report.txt", "a");
        string token = await ShareAsync(file.Id);
        string adminId = (await Client.FindUserAsync(TestServerFactory.AdminUsername)).Id;
        _logs.Clear();

        await Client.GetAsync($"/api/shares/{token}/items/{file.Id}/view", CancellationToken);

        CapturedLog log = Assert.Single(Audit);
        Assert.Equal(9040, log.EventId.Id);
        Assert.Equal(adminId, log.Properties["ActorId"]);
        Assert.DoesNotContain("anonymously", log.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LogsPreviewsAtDebugWhenTheAuditCategoryIsRaisedToIt()
    {
        await using TestServerFactory own = new()
        {
            Settings = new Dictionary<string, string> { ["Logging:LogLevel:Guardou.Audit"] = "Debug" },
            TestServices = services => services.AddLogging(logging => logging.AddProvider(_logs)),
        };
        await own.EnsureAdministratorAsync();
        using HttpClient admin = own.CreateClient();
        await admin.LoginAsync(TestServerFactory.AdminUsername, TestServerFactory.AdminPassword);
        ItemEntry image = await admin.UploadImageAsync("wide.png");
        await admin.WaitForPreviewAsync(image.Id);
        HttpResponseMessage share = await admin.PutAsync($"/api/items/{image.Id}/share", null, CancellationToken);
        ShareLink link = await share.Content.ReadFromJsonAsync<ShareLink>(HttpClientExtensions.JsonOptions, CancellationToken)
            ?? throw new InvalidOperationException("The share link is missing.");
        string token = link.Url[(link.Url.LastIndexOf('/') + 1)..];
        _logs.Clear();

        await own.CreateClient().GetAsync($"/api/shares/{token}/items/{image.Id}/preview?size=thumbnail", CancellationToken);

        CapturedLog log = Assert.Single(Audit);
        Assert.Equal(9041, log.EventId.Id);
        Assert.Equal(LogLevel.Debug, log.Level);
        Assert.DoesNotContain(log.Texts, text => text.Contains(token, StringComparison.Ordinal));
    }

    [Fact]
    public async Task DoesNotLogPreviewsAtTheDefaultLevel()
    {
        ItemEntry image = await Client.UploadImageAsync("wide.png");
        await Client.WaitForPreviewAsync(image.Id);
        string token = await ShareAsync(image.Id);
        _logs.Clear();

        HttpResponseMessage preview = await Factory.CreateClient().GetAsync($"/api/shares/{token}/items/{image.Id}/preview?size=thumbnail", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Empty(Audit);
    }

    private protected override TestServerFactory CreateFactory()
    {
        return FactoryWith(trustedProxies: "10.0.0.0/8");
    }

    private HttpClient ClientBehindProxy()
    {
        HttpClient client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add(RemoteAddressFilter.Header, "10.1.2.3");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ForwardedClient);
        return client;
    }

    private TestServerFactory FactoryWith(string? trustedProxies)
    {
        return new TestServerFactory
        {
            Settings = trustedProxies is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { [TrustedProxiesKey] = trustedProxies },
            TestServices = services =>
            {
                services.AddSingleton<IStartupFilter, RemoteAddressFilter>();
                services.AddLogging(logging => logging.AddProvider(_logs));
            },
        };
    }

    private async Task<string> ShareAsync(string id)
    {
        HttpResponseMessage response = await Client.PutAsync($"/api/items/{id}/share", null, CancellationToken);
        response.EnsureSuccessStatusCode();
        ShareLink link = await response.Content.ReadFromJsonAsync<ShareLink>(HttpClientExtensions.JsonOptions, CancellationToken)
            ?? throw new InvalidOperationException("The share link is missing.");
        return link.Url[(link.Url.LastIndexOf('/') + 1)..];
    }

    private async Task LinkLoginAsync(string userId)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        UserAccountResult result = await scope.ServiceProvider.GetRequiredService<IUserAccountStore>()
            .AddLoginAsync(userId, OidcSettings.Provider, "subject-1");
        Assert.True(result.Succeeded);
    }

    private async Task<Guid> ShareIdAsync(string token)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        Share share = await scope.ServiceProvider.GetRequiredService<IShareStore>().FindByTokenAsync(token, CancellationToken)
            ?? throw new InvalidOperationException("The share is missing.");
        return share.Id;
    }
}
