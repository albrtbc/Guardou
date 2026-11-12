using Guardou.Domain.Files;

namespace Guardou.Application.Search;

internal sealed class SearchQueryParser(TimeProvider time)
{
    private const char KeySeparator = ':';
    private const string TypeKey = "type";
    private const string ModifiedKey = "modified";
    private const string SizeKey = "size";
    private const string IsKey = "is";
    private const string StarredState = "starred";

    public SearchQuery Parse(string? text)
    {
        ModifiedFilter modified = new(time.GetUtcNow(), time.LocalTimeZone);
        List<string> terms = [];
        HashSet<ItemType> types = [];
        Period period = new(null, null);
        SizeRange size = new(null, null);
        bool starred = false;
        foreach (SearchToken token in SearchTokenizer.Tokenize(ItemName.TryNormalize(text ?? string.Empty) ?? string.Empty))
        {
            (string key, string value) = Split(token);
            switch (key)
            {
                case TypeKey:
                    types.Add(ParseType(value));
                    break;
                case ModifiedKey:
                    period = Intersect(period, modified.Parse(value));
                    break;
                case SizeKey:
                    size = Intersect(size, SizeFilter.Parse(value));
                    break;
                case IsKey:
                    starred = IsStarred(value);
                    break;
                default:
                    terms.Add(token.Value);
                    break;
            }
        }

        return new SearchQuery(terms, types, period.From, period.Before, size.LargerThan, size.SmallerThan, starred);
    }

    private static (string Key, string Value) Split(SearchToken token)
    {
        int separator = token.Value.IndexOf(KeySeparator, StringComparison.Ordinal);
        return token.Quoted || separator <= 0
            ? (string.Empty, token.Value)
            : (token.Value[..separator].ToLowerInvariant(), token.Value[(separator + 1)..]);
    }

    private static ItemType ParseType(string value)
    {
        return value.All(char.IsAsciiLetter) && Enum.TryParse(value, ignoreCase: true, out ItemType type)
            ? type
            : throw new InvalidSearchQueryException($"The type '{value}' is not valid.");
    }

    private static bool IsStarred(string value)
    {
        if (!value.Equals(StarredState, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidSearchQueryException($"The state '{value}' is not valid.");
        }

        return true;
    }

    private static Period Intersect(Period current, Period added)
    {
        return new Period(Larger(current.From, added.From), Smaller(current.Before, added.Before));
    }

    private static SizeRange Intersect(SizeRange current, SizeRange added)
    {
        return new SizeRange(Larger(current.LargerThan, added.LargerThan), Smaller(current.SmallerThan, added.SmallerThan));
    }

    private static T? Larger<T>(T? first, T? second)
        where T : struct, IComparable<T>
    {
        return Pick(first, second, comparison => comparison >= 0);
    }

    private static T? Smaller<T>(T? first, T? second)
        where T : struct, IComparable<T>
    {
        return Pick(first, second, comparison => comparison <= 0);
    }

    private static T? Pick<T>(T? first, T? second, Func<int, bool> keepFirst)
        where T : struct, IComparable<T>
    {
        if (first is not { } a)
        {
            return second;
        }

        return second is { } b && !keepFirst(a.CompareTo(b)) ? b : a;
    }
}
