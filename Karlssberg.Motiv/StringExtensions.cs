namespace Karlssberg.Motiv;

/// <summary>
/// Provides extension methods for strings.
/// </summary>
public static class StringExtensions
{
    private static readonly HashSet<char> Characters = ['!', '(', ')', '&', '|', '^'];

    /// <summary>
    /// Serializes a collection to a human-readable string.  It will separate the items with <c>", "</c> and the last
    /// item with <c>", and "</c>.
    /// </summary>
    /// <param name="collection">The collection to serialize.</param>
    /// <returns>The serialized string.</returns>
    public static string Serialize<T>(
        this IEnumerable<T> collection) =>
        string.Concat(collection.SerializeToEnumerable(",", "and"));

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
        string.Concat(collection.SerializeToEnumerable(",", conjunction));

    /// <summary>Serializes a collection to a human-readable string.</summary>
    /// <param name="collection">The collection to serialize.</param>
    /// <param name="delimiter">The delimiter to use other than the default comma</param>
    /// <param name="conjunction">The coordinating conjunction (i.e. "and", "or" etc) to be used between the last two items</param>
    /// <param name="useOxfordComma">
    /// A value indicating whether to use a comma as well as a conjunction between the last two
    /// items.
    /// </param>
    /// <returns>The serialized string.</returns>
    public static string Serialize<T>(
        this IEnumerable<T> collection,
        string delimiter,
        string conjunction,
        bool useOxfordComma = true)
    {
        var strings = collection
            .SerializeToEnumerable(
                delimiter,
                conjunction);

        return string.Concat(strings);
    }

    private static IEnumerable<string?> SerializeToEnumerable<T>(
        this IEnumerable<T> collection,
        string delimiter,
        string? conjunction)
    {
        const int bufferSize = 2;
        var buffer = new Queue<string?>(bufferSize);
        var size = 0;
        foreach (var item in collection)
        {
            size++;
            buffer.Enqueue(item?.ToString());
            if (buffer.Count <= bufferSize)
                continue;

            yield return buffer.Dequeue();
            yield return delimiter;
            yield return " ";
        }
        
        switch (size)
        {
            case 0:
                yield break;
            case 1:
                yield return buffer.Dequeue();
                yield break;
            case 2:
                yield return buffer.Dequeue();
                yield return " ";
                yield return conjunction;
                yield return " ";
                yield return buffer.Dequeue();
                yield break;
            default:
                yield return buffer.Dequeue();
                yield return delimiter;
                yield return " ";
                yield return conjunction;
                yield return " ";
                yield return buffer.Dequeue();
                yield break;
        }
    }

    internal static bool ContainsReservedCharacters(this string text) => text.Any(c => Characters.Contains(c));
}