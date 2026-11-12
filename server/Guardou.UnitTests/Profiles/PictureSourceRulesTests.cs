using System.Net;
using Guardou.Infrastructure.Profiles;

namespace Guardou.UnitTests.Profiles;

public sealed class PictureSourceRulesTests
{
    [Theory]
    [InlineData("https://id.example.com/picture.png", true)]
    [InlineData("http://id.example.com/picture.png", false)]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("ftp://id.example.com/picture.png", false)]
    public void PermitsOnlyHttps(string source, bool permitted)
    {
        Assert.Equal(permitted, PictureSourceRules.Permits(new Uri(source)));
    }

    [Theory]
    [InlineData("93.184.215.14", true)]
    [InlineData("172.32.0.1", true)]
    [InlineData("2606:4700::1111", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("10.1.2.3", false)]
    [InlineData("172.16.0.1", false)]
    [InlineData("172.31.255.255", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("::", false)]
    [InlineData("fe80::1", false)]
    [InlineData("fd00::1", false)]
    [InlineData("::ffff:127.0.0.1", false)]
    [InlineData("::ffff:192.168.0.1", false)]
    public void RefusesLoopbackPrivateAndLinkLocalAddresses(string address, bool permitted)
    {
        Assert.Equal(permitted, PictureSourceRules.Permits(IPAddress.Parse(address)));
    }

    [Fact]
    public async Task ConnectorRefusesALoopbackHost()
    {
        using HttpClient client = new(PublicAddressConnector.CreateHandler());

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAsync(new Uri("https://localhost/picture.png"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void HandlerDoesNotFollowRedirects()
    {
        using SocketsHttpHandler handler = PublicAddressConnector.CreateHandler();

        Assert.False(handler.AllowAutoRedirect);
    }
}
