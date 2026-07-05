using System.Text;

namespace Motiv;

/// <summary>
/// Provides extension methods for strings.
/// </summary>
public static class StringExtensions
{
    private static readonly HashSet<char> Characters = ['!', '(', ')', '&', '|', '^', '¬' ];

    /// <summary>
    /// Serializes a collection to a human-readable string.  It will separate the items with <c>", "</c> and the last
    /// item with <c>", and "</c>.
    /// </summary>
    /// <param name="collection">The collection to serialize.</param>
    /// <returns>The serialized string.</returns>
    public static string Serialize<T>(
        this IEnumerable<T> collection) =>
        collection.SerializeToString(",", "and", true);

    /// <summary>Serializes a collection to a human-readable string.</summary>
    /// <param name="collection">The collection to serialize.</param>
    /// <param name="conjunction">The coordinating conjunction (i.e. "and", "or" etc) to be used between the last two items </param>
    /// <param name="useOxfordComma">
    /// A value indicating whether to use a comma as well as a conjunction between the last two
    /// items.
    /// </param>
    /// <returns>The serialized string.</returns>
    public static string Serialize<T>(
        this IEnumerable<T> collection,
        string conjunction,
        bool useOxfordComma = true) =>
        collection.SerializeToString(",", conjunction, useOxfordComma);

    private static string SerializeToString<T>(
        this IEnumerable<T> collection,
        string delimiter,
        string? conjunction,
        bool useOxfordComma)
    {
        using var enumerator = collection.GetEnumerator();
        if (!enumerator.MoveNext())
            return string.Empty;

        var previous = enumerator.Current?.ToString();
        if (!enumerator.MoveNext())
            return previous ?? string.Empty;

        var builder = new StringBuilder();
        var current = enumerator.Current?.ToString();
        var count = 2;
        while (enumerator.MoveNext())
        {
            builder.Append(previous).Append(delimiter).Append(' ');
            previous = current;
            current = enumerator.Current?.ToString();
            count++;
        }

        builder.Append(previous);
        if (count > 2 && useOxfordComma)
            builder.Append(delimiter);

        return builder
            .Append(' ')
            .Append(conjunction)
            .Append(' ')
            .Append(current)
            .ToString();
    }

    internal static bool ContainsReservedCharacters(this string text)
    {
        foreach (var c in text)
            if (Characters.Contains(c))
                return true;

        return false;
    }

    internal static bool EndsWithEqualityAssertion(this string text) =>
        text.EndsWith(" == false") || text.EndsWith(" == true");

    internal static string AsUnsatisfied(this string text) =>
        text switch
        {
            _ when text.EndsWith(" == true") => text.Substring(0, text.Length - " == true".Length) + " == false",
            _ when text.EndsWith(" == false") => text.Substring(0, text.Length - " == false".Length) + " == true",
            _ => $"{text} == false"
        };

    internal static string AsSatisfied(this string text) =>
        text.EndsWithEqualityAssertion() ? text : $"{text} == true";
}
