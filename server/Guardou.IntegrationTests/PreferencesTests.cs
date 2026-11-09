using System.Net;
using System.Net.Http.Json;
using System.Text;
using Guardou.Domain.Users;

namespace Guardou.IntegrationTests;

public sealed class PreferencesTests : SignedInTests
{
    private const string PreferencesPath = "/api/users/me/preferences";

    [Fact]
    public async Task GivesTheDefaultsToANewUser()
    {
        UserPreferences? preferences = await Client.GetFromJsonAsync<UserPreferences>(PreferencesPath, HttpClientExtensions.JsonOptions, CancellationToken);
        UserPreferences? defaults = await Client.GetFromJsonAsync<UserPreferences>($"{PreferencesPath}/defaults", HttpClientExtensions.JsonOptions, CancellationToken);

        Assert.Equal(UserPreferences.Defaults, preferences);
        Assert.Equal(UserPreferences.Defaults, defaults);
    }

    [Fact]
    public async Task KeepsTheChangedPreferencesForEachUser()
    {
        UserPreferences changed = new(ThemePreference.Dark, Language.PtBr, StartPage.Suggested, UploadConflictPolicy.Rename);

        HttpResponseMessage response = await Client.PutAsJsonAsync(PreferencesPath, changed, HttpClientExtensions.JsonOptions, CancellationToken);
        using HttpClient bob = await SignInAsBobAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(changed, await response.Content.ReadFromJsonAsync<UserPreferences>(HttpClientExtensions.JsonOptions, CancellationToken));
        Assert.Equal(changed, await Client.GetFromJsonAsync<UserPreferences>(PreferencesPath, HttpClientExtensions.JsonOptions, CancellationToken));
        Assert.Equal(UserPreferences.Defaults, await bob.GetFromJsonAsync<UserPreferences>(PreferencesPath, HttpClientExtensions.JsonOptions, CancellationToken));
    }

    [Fact]
    public async Task WritesTheValuesAsText()
    {
        await Client.PutAsJsonAsync(PreferencesPath, new UserPreferences(ThemePreference.Light, Language.PtBr, StartPage.Starred, UploadConflictPolicy.Skip), HttpClientExtensions.JsonOptions, CancellationToken);

        string json = await Client.GetStringAsync(PreferencesPath, CancellationToken);

        Assert.Equal("""{"theme":"light","language":"ptBr","startPage":"starred","uploadConflictPolicy":"skip"}""", json);
    }

    [Fact]
    public async Task RefusesAnUnknownValue()
    {
        using StringContent body = new("""{"theme":"blue","language":"en","startPage":"files","uploadConflictPolicy":"ask"}""", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await Client.PutAsync(PreferencesPath, body, CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RefusesAnAnonymousRequest()
    {
        using HttpClient anonymous = Factory.CreateClient();

        HttpResponseMessage response = await anonymous.GetAsync(PreferencesPath, CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
