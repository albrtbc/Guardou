using Guardou.Domain.Files;

namespace Guardou.Application.Files;

internal sealed class CaseInsensitiveNames
{
    private readonly HashSet<string> _used = new(StringComparer.OrdinalIgnoreCase);

    public string Claim(string name)
    {
        string candidate = name;
        for (int number = 1; !_used.Add(candidate); number++)
        {
            candidate = ItemName.Numbered(name, number);
        }

        return candidate;
    }
}
