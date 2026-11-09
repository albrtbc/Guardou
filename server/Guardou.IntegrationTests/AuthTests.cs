using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Guardou.Api.Models;
using Guardou.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Guardou.IntegrationTests;

public sealed class AuthTests(TestServerFactory factory) : IClassFixture<TestServerFactory>
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MeRequiresASession()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/api/auth/me", CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task LoginRequiresAnAntiforgeryToken()
    {
        HttpResponseMessage response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(TestServerFactory.AdminUsername, TestServerFactory.AdminPassword, RememberMe: false),
            CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LoginValidatesTheRequest()
    {
        HttpResponseMessage response = await factory.CreateClient().LoginAsync(string.Empty, string.Empty);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task LoginRefusesAWrongPassword()
    {
        await factory.EnsureAdministratorAsync();
        HttpResponseMessage response = await factory.CreateClient().LoginAsync(TestServerFactory.AdminUsername, "wrong-password");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task LoginStartsASessionForTheAdministrator()
    {
        await factory.EnsureAdministratorAsync();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage login = await client.LoginAsync(TestServerFactory.AdminUsername, TestServerFactory.AdminPassword);
        CurrentUser? me = await client.GetFromJsonAsync<CurrentUser>("/api/auth/me", CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        Assert.Equal(new CurrentUser(
            me?.Id ?? string.Empty,
            TestServerFactory.AdminUsername,
            TestServerFactory.AdminDisplayName,
            TestServerFactory.AdminEmail,
            IsAdmin: true,
            HasPicture: false), me);
        Assert.NotEmpty(me?.Id ?? string.Empty);
    }

    [Fact]
    public async Task LogoutEndsTheSession()
    {
        await factory.EnsureAdministratorAsync();
        HttpClient client = factory.CreateClient();
        await client.LoginAsync(TestServerFactory.AdminUsername, TestServerFactory.AdminPassword);

        HttpResponseMessage logout = await client.PostAsync("/api/auth/logout", content: null, CancellationToken);
        LogoutResult? result = await logout.Content.ReadFromJsonAsync<LogoutResult>(CancellationToken);
        HttpResponseMessage me = await client.GetAsync("/api/auth/me", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Null(result?.RedirectUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task LockoutRefusesTheCorrectPasswordLikeAWrongOneUntilItExpires()
    {
        await using TestServerFactory own = new();
        await own.EnsureAdministratorAsync();
        using HttpClient client = own.CreateClient();

        HttpResponseMessage wrong = new();
        for (int attempt = 0; attempt < 5; attempt++)
        {
            wrong = await client.LoginAsync(TestServerFactory.AdminUsername, "wrong-password");
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        HttpResponseMessage locked = await client.LoginAsync(TestServerFactory.AdminUsername, TestServerFactory.AdminPassword);
        HttpResponseMessage unknown = await client.LoginAsync("nobody", "wrong-password");
        DateTimeOffset? lockoutEnd = (await FindAdministratorAsync(own)).LockoutEnd;

        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal(await ProblemAsync(wrong), await ProblemAsync(locked));
        Assert.Equal(await ProblemAsync(wrong), await ProblemAsync(unknown));
        Assert.True(lockoutEnd > DateTimeOffset.UtcNow.AddMinutes(14));

        // Identity reads the system clock, not the test TimeProvider, so the lockout is ended in the database.
        await UpdateAdministratorAsync(own, (users, admin) => users.SetLockoutEndDateAsync(admin, DateTimeOffset.UtcNow.AddMinutes(-1)));
        HttpResponseMessage unlocked = await client.LoginAsync(TestServerFactory.AdminUsername, TestServerFactory.AdminPassword);

        Assert.Equal(HttpStatusCode.NoContent, unlocked.StatusCode);
    }

    [Fact]
    public async Task LockoutAppliesToAnAccountThatHadItDisabled()
    {
        await using TestServerFactory own = new();
        await own.EnsureAdministratorAsync();
        using HttpClient client = own.CreateClient();
        await UpdateAdministratorAsync(own, (users, admin) => users.SetLockoutEnabledAsync(admin, enabled: false));

        for (int attempt = 0; attempt < 5; attempt++)
        {
            await client.LoginAsync(TestServerFactory.AdminUsername, "wrong-password");
        }

        HttpResponseMessage locked = await client.LoginAsync(TestServerFactory.AdminUsername, TestServerFactory.AdminPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
    }

    [Fact]
    public async Task ASuccessfulLoginResetsTheFailedAttempts()
    {
        await using TestServerFactory own = new();
        await own.EnsureAdministratorAsync();
        using HttpClient client = own.CreateClient();

        for (int attempt = 0; attempt < 3; attempt++)
        {
            await client.LoginAsync(TestServerFactory.AdminUsername, "wrong-password");
        }

        await client.LoginAsync(TestServerFactory.AdminUsername, TestServerFactory.AdminPassword);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            await client.LoginAsync(TestServerFactory.AdminUsername, "wrong-password");
        }

        HttpResponseMessage login = await client.LoginAsync(TestServerFactory.AdminUsername, TestServerFactory.AdminPassword);

        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
    }

    [Fact]
    public async Task PasswordsAreHashedWithTheConfiguredCost()
    {
        await using TestServerFactory own = new();
        await own.EnsureAdministratorAsync();

        byte[] hash = Convert.FromBase64String((await FindAdministratorAsync(own)).PasswordHash ?? string.Empty);

        Assert.Equal(1, hash[0]);
        Assert.Equal(210_000, BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(5, 4)));
    }

    [Fact]
    public async Task RefusalsThatNeedNoRealCheckCostAsMuchAsAWrongPassword()
    {
        int verifications = 0;
        await using TestServerFactory own = new()
        {
            TestServices = services => services.Replace(ServiceDescriptor.Scoped<IPasswordHasher<ApplicationUser>>(
                _ => new CountingPasswordHasher(() => verifications++))),
        };
        await own.EnsureAdministratorAsync();
        using HttpClient client = own.CreateClient();

        verifications = 0;
        await client.LoginAsync("nobody", "wrong-password");
        int unknown = verifications;
        verifications = 0;
        await client.LoginAsync(TestServerFactory.AdminUsername, "wrong-password");
        int wrong = verifications;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            await client.LoginAsync(TestServerFactory.AdminUsername, "wrong-password");
        }

        verifications = 0;
        using HttpResponseMessage lockedOut = await client.LoginAsync(TestServerFactory.AdminUsername, TestServerFactory.AdminPassword);

        Assert.Equal(1, unknown);
        Assert.Equal(1, wrong);
        Assert.Equal(1, verifications);
        Assert.Equal(HttpStatusCode.Unauthorized, lockedOut.StatusCode);
    }

    [Fact]
    public async Task ASessionEndsThirtyDaysAfterSignInEvenWhileInUse()
    {
        await using TestServerFactory own = new();
        await own.EnsureAdministratorAsync();
        using HttpClient client = own.CreateClient();
        await client.LoginAsync(TestServerFactory.AdminUsername, TestServerFactory.AdminPassword);

        for (int day = 5; day <= 25; day += 5)
        {
            own.Time.Advance(TimeSpan.FromDays(5));
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me", CancellationToken)).StatusCode);
        }

        own.Time.Advance(TimeSpan.FromDays(6));
        HttpResponseMessage expired = await client.GetAsync("/api/auth/me", CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
    }

    private static async Task<ApplicationUser> FindAdministratorAsync(TestServerFactory factory)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        return await FindAdministratorAsync(scope);
    }

    private static async Task UpdateAdministratorAsync(TestServerFactory factory, Func<UserManager<ApplicationUser>, ApplicationUser, Task> update)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        await update(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(), await FindAdministratorAsync(scope));
    }

    private static async Task<ApplicationUser> FindAdministratorAsync(AsyncServiceScope scope)
    {
        UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await users.FindByNameAsync(TestServerFactory.AdminUsername) ?? throw new InvalidOperationException("The administrator does not exist.");
    }

    private static async Task<string> ProblemAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        return $"{problem.RootElement.GetProperty("status")} {problem.RootElement.GetProperty("title")}";
    }

    private sealed class CountingPasswordHasher(Action verified) : IPasswordHasher<ApplicationUser>
    {
        private readonly PasswordHasher<ApplicationUser> _inner = new();

        public string HashPassword(ApplicationUser user, string password)
        {
            return _inner.HashPassword(user, password);
        }

        public PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hashedPassword, string providedPassword)
        {
            verified();
            return _inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }
}
