using System.Text.RegularExpressions;

namespace Guardou.Api.Errors;

// A share token is a credential, so any request path that reaches a log has it replaced,
// in the API routes (/api/shares/{token}) and in the share page (/s/{token}).
internal static partial class RequestPathRedaction
{
    private const string Redacted = "***";

    public static string Redact(PathString path)
    {
        return SharePath().Replace(path.Value ?? string.Empty, "${prefix}" + Redacted);
    }

    [GeneratedRegex("^(?<prefix>/(?:api/shares|s)/)[^/]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SharePath();
}
