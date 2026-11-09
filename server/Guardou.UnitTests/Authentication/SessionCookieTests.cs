using Guardou.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;

namespace Guardou.UnitTests.Authentication;

public sealed class SessionCookieTests
{
    private static readonly DateTimeOffset SignedIn = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0)]
    [InlineData(29)]
    [InlineData(30)]
    public void KeepsASessionUpToThirtyDaysOld(int days)
    {
        AuthenticationProperties properties = Stamped();

        Assert.False(SessionCookie.HasExceededMaxAge(properties, SignedIn.AddDays(days)));
    }

    [Fact]
    public void RefusesASessionOlderThanThirtyDays()
    {
        AuthenticationProperties properties = Stamped();

        Assert.True(SessionCookie.HasExceededMaxAge(properties, SignedIn.AddDays(30).AddSeconds(1)));
    }

    [Fact]
    public void RefusesASessionWithoutASignInTime()
    {
        Assert.True(SessionCookie.HasExceededMaxAge(new AuthenticationProperties(), SignedIn));
        Assert.True(SessionCookie.HasExceededMaxAge(null, SignedIn));
    }

    [Theory]
    [InlineData("")]
    [InlineData("yesterday")]
    [InlineData("-1")]
    public void RefusesASessionWithAnUnreadableSignInTime(string value)
    {
        AuthenticationProperties properties = new();
        properties.Items["auth_time"] = value;

        Assert.True(SessionCookie.HasExceededMaxAge(properties, SignedIn));
    }

    private static AuthenticationProperties Stamped()
    {
        AuthenticationProperties properties = new();
        SessionCookie.StampAuthTime(properties, SignedIn);
        return properties;
    }
}
