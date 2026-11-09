using System.Net;
using System.Net.Http.Json;
using Guardou.Api.Models;
using Guardou.Application.Users;
using NetVips;

namespace Guardou.IntegrationTests;

public sealed class ProfilePicturesTests : SignedInTests
{
    [Fact]
    public async Task UserReplacesTheOwnPicture()
    {
        HttpClient bob = await SignInAsBobAsync();
        UserAccount account = await Client.FindUserAsync(BobUsername);

        HttpResponseMessage response = await bob.ReplacePictureAsync(account.Id, PreviewsApi.Png(800, 400));
        HttpResponseMessage picture = await bob.GetPictureAsync(account.Id);
        CurrentUser? me = await bob.GetFromJsonAsync<CurrentUser>("/api/auth/me", CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("image/webp", picture.Content.Headers.ContentType?.MediaType);
        using Image image = Image.NewFromBuffer(await picture.Content.ReadAsByteArrayAsync(CancellationToken));
        Assert.Equal((256, 256), (image.Width, image.Height));
        Assert.True(me?.HasPicture);
    }

    [Fact]
    public async Task UserCannotReplaceThePictureOfAnotherUser()
    {
        HttpClient bob = await SignInAsBobAsync();
        UserAccount admin = await Client.FindUserAsync(TestServerFactory.AdminUsername);

        HttpResponseMessage replace = await bob.ReplacePictureAsync(admin.Id, PreviewsApi.Png(64, 64));
        HttpResponseMessage delete = await bob.DeletePictureAsync(admin.Id);

        Assert.Equal(HttpStatusCode.Forbidden, replace.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task AdminReplacesThePictureOfAnotherUser()
    {
        UserAccount account = await Client.FindUserAsync(BobUsername);

        HttpResponseMessage response = await Client.ReplacePictureAsync(account.Id, PreviewsApi.Png(64, 64));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True((await Client.FindUserAsync(BobUsername)).HasPicture);
    }

    [Fact]
    public async Task ReplaceRefusesContentThatIsNotAnImage()
    {
        UserAccount admin = await Client.FindUserAsync(TestServerFactory.AdminUsername);

        HttpResponseMessage response = await Client.ReplacePictureAsync(admin.Id, [1, 2, 3]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False((await Client.FindUserAsync(TestServerFactory.AdminUsername)).HasPicture);
    }

    [Fact]
    public async Task ReplaceRefusesALargePicture()
    {
        UserAccount admin = await Client.FindUserAsync(TestServerFactory.AdminUsername);

        HttpResponseMessage response = await Client.ReplacePictureAsync(admin.Id, new byte[ProfilePictureLimits.MaximumBytes + 1]);

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge);
        Assert.False((await Client.FindUserAsync(TestServerFactory.AdminUsername)).HasPicture);
    }

    [Fact]
    public async Task ReplaceRefusesAPictureOverThePixelLimit()
    {
        UserAccount admin = await Client.FindUserAsync(TestServerFactory.AdminUsername);

        HttpResponseMessage response = await Client.ReplacePictureAsync(admin.Id, PreviewsApi.LargePng(20_000, 20_000));
        string body = await response.Content.ReadAsStringAsync(CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("megapixels", body, StringComparison.Ordinal);
        Assert.False((await Client.FindUserAsync(TestServerFactory.AdminUsername)).HasPicture);
    }

    [Fact]
    public async Task ReplaceRefusesAMissingUser()
    {
        HttpResponseMessage response = await Client.ReplacePictureAsync("missing", PreviewsApi.Png(64, 64));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteRemovesThePicture()
    {
        UserAccount admin = await Client.FindUserAsync(TestServerFactory.AdminUsername);
        await Client.ReplacePictureAsync(admin.Id, PreviewsApi.Png(64, 64));

        HttpResponseMessage response = await Client.DeletePictureAsync(admin.Id);
        HttpResponseMessage picture = await Client.GetPictureAsync(admin.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, picture.StatusCode);
    }
}
