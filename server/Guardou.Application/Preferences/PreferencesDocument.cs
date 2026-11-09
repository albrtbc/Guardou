using Guardou.Domain.Users;

namespace Guardou.Application.Preferences;

public sealed record PreferencesDocument(ThemePreference? Theme, Language? Language, StartPage? StartPage, UploadConflictPolicy? UploadConflictPolicy)
{
    public static PreferencesDocument Of(UserPreferences preferences)
    {
        return new PreferencesDocument(preferences.Theme, preferences.Language, preferences.StartPage, preferences.UploadConflictPolicy);
    }

    public UserPreferences MergeOver(UserPreferences defaults)
    {
        return new UserPreferences(
            Theme ?? defaults.Theme,
            Language ?? defaults.Language,
            StartPage ?? defaults.StartPage,
            UploadConflictPolicy ?? defaults.UploadConflictPolicy);
    }
}
