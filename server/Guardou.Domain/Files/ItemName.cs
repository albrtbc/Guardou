using System.Text;

namespace Guardou.Domain.Files;

public static class ItemName
{
    public const int MaximumLength = 255;

    private static readonly string[] ReservedNames = [".", ".."];
    private static readonly char[] ForbiddenCharacters = ['/', '\\'];

    public static bool IsValid(string name)
    {
        return name.Length is > 0 and <= MaximumLength
            && name.Trim() == name
            && !ReservedNames.Contains(name)
            && name.IndexOfAny(ForbiddenCharacters) < 0
            && !name.Any(char.IsControl);
    }

    public static string? TryNormalize(string name)
    {
        try
        {
            return name.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public static string Numbered(string name, int number)
    {
        int dot = name.LastIndexOf('.');
        return dot > 0
            ? $"{name[..dot]} ({number}){name[dot..]}"
            : $"{name} ({number})";
    }

    public static string Extension(string name)
    {
        int dot = name.LastIndexOf('.');
        return dot > 0 ? name[(dot + 1)..] : string.Empty;
    }
}
