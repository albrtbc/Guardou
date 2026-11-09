namespace Guardou.Domain.Users;

public sealed record UserPreferences(ThemePreference Theme, Language Language, StartPage StartPage, UploadConflictPolicy UploadConflictPolicy)
{
    public static UserPreferences Defaults { get; } = new(ThemePreference.System, Language.System, StartPage.Files, UploadConflictPolicy.Ask);
}
