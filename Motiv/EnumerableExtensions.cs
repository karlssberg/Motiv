namespace Motiv;

/// <summary>
/// Provides extension methods for <see cref="IEnumerable{T}"/>.
/// </summary>
public static class EnumerableExtensions
{
    /// <summary>
    /// Enumerates a single item as an <See cref="IEnumerable{T}"/>.
    /// </summary>
    /// <param name="item">The item to enumerate.</param>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <returns>An <See cref="IEnumerable{T}"/> containing only the item provided.</returns>
    public static IEnumerable<T> ToEnumerable<T>(this T item)
    {
        yield return item;
    }

    internal static IEnumerable<T> ReplaceFirstLine<T>(this IEnumerable<T> lines, Func<T, T> prefixFn)
    {
        using var enumerator = lines.GetEnumerator();
        if (!enumerator.MoveNext())
            yield break;

        var firstLine = enumerator.Current;
        yield return prefixFn(firstLine);

        while (enumerator.MoveNext())
            yield return enumerator.Current;
    }

    internal static IEnumerable<T> ElseIfEmpty<T>(
        this IEnumerable<T> source,
        IEnumerable<T> alternative)
    {
        using var sourceEnumerator = source.GetEnumerator();
        var sourceHasItems = sourceEnumerator.MoveNext();
        
        if (sourceHasItems)
            yield return sourceEnumerator.Current;
        
        while (sourceEnumerator.MoveNext())
            yield return sourceEnumerator.Current;
        
        if (sourceHasItems)
            yield break;
        
        using var alternativeEnumerator = alternative.GetEnumerator();
        while (alternativeEnumerator.MoveNext())
            yield return alternativeEnumerator.Current;
    }

    internal static bool HasAtLeast<T>(this IEnumerable<T> source, int n)
    {
        if (source == null)
            throw new ArgumentNullException(paramName: nameof(source));

        using var enumerator = source.GetEnumerator();
        
        for (var i = 0; i < n; i++)
            if (!enumerator.MoveNext())
                return false;

        return true;
    }

    internal static IEnumerable<IEnumerable<T>> GroupAdjacentBy<T>(
        this IEnumerable<T> source, Func<T, T, bool> predicate)
    {
        using var enumerator = source.GetEnumerator();
        if (!enumerator.MoveNext())
            yield break;

        List<T> currentGroup = [enumerator.Current];
        var prev = enumerator.Current;

        while (enumerator.MoveNext())
        {
            if (predicate(prev, enumerator.Current))
            {
                currentGroup.Add(enumerator.Current);
            }
            else
            {
                yield return currentGroup;
                currentGroup = [enumerator.Current];
            }
            prev = enumerator.Current;
        }

        yield return currentGroup;
    }

    internal static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> source) =>
        source.Select((item, index) => (item, index));
}