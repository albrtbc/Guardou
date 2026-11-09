using Guardou.Application.Preferences;
using Guardou.Domain.Users;

namespace Guardou.UnitTests.Preferences;

public sealed class PreferencesDocumentTests
{
    private static readonly UserPreferences Defaults = new(ThemePreference.Dark, Language.Pt, StartPage.Starred, UploadConflictPolicy.Rename);

    [Fact]
    public void AnEmptyDocumentGivesTheDefaults()
    {
        UserPreferences merged = new PreferencesDocument(null, null, null, null).MergeOver(Defaults);

        Assert.Equal(Defaults, merged);
    }

    [Fact]
    public void StoredValuesReplaceTheDefaults()
    {
        UserPreferences merged = new PreferencesDocument(ThemePreference.Light, null, StartPage.Suggested, UploadConflictPolicy.Skip).MergeOver(Defaults);

        Assert.Equal(new UserPreferences(ThemePreference.Light, Language.Pt, StartPage.Suggested, UploadConflictPolicy.Skip), merged);
    }

    [Fact]
    public void AFullDocumentKeepsAllValues()
    {
        UserPreferences preferences = new(ThemePreference.System, Language.PtBr, StartPage.Shared, UploadConflictPolicy.Replace);

        Assert.Equal(preferences, PreferencesDocument.Of(preferences).MergeOver(Defaults));
    }
}
