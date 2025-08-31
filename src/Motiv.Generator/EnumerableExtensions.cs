using Microsoft.CodeAnalysis;

namespace Motiv.Generator;

public static class EnumerableExtensions
{
    public static IEnumerable<SyntaxNodeOrToken> InterleaveWith(this IEnumerable<SyntaxNode> source, SyntaxNodeOrToken value)
    {
        using var enumerator = source.GetEnumerator();

        if (!enumerator.MoveNext())
        {
            yield break;
        }

        yield return enumerator.Current;
        while (enumerator.MoveNext())
        {
            yield return value;
            yield return enumerator.Current;
        }
    }

    public static IEnumerable<T> AppendIfNotNull<T>(this IEnumerable<T> source, T? value)
        where T : class
    {
        return value is null
            ? source
            : source.Append(value);
    }

    public static IEnumerable<TValue> DistinctBy<TKey, TValue>(this IEnumerable<TValue> types, Func<TValue, TKey> keySelector)
    {
        var set = new HashSet<TKey>();
        foreach (var type in types)
        {
            if (set.Add(keySelector(type)))
            {
                yield return type;
            }
        }
    }

    public static IList<T> AddRange<T>(this IList<T> source, IEnumerable<T> values)
    {
        foreach (var value in values)
        {
            source.Add(value);
        }

        return source;
    }
}
