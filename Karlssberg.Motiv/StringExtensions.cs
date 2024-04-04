using System.Text;

namespace Karlssberg.Motiv;

internal static class StringExtensions
{
    internal static bool IsBracketed(this string value) =>
        (value.StartsWith("(") || value.StartsWith("!(")) && value.EndsWith(")");
    
    internal static bool IsLongExpression(this string value) => value.Length > 20;
    
    internal static string EnsureBracketed(this string value) => 
        !value.IsBracketed() || value.RequiresBrackets()
            ? $"({value})"
            : value;

    internal static bool RequiresBrackets(this string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        
        var currentLevel = 0;
        for (var index = 0; index < text.Length; index++)
        {
            switch (text[index])
            {
                case '!': 
                    return currentLevel <= 0;
                case '(':
                    currentLevel++;
                    break;
                case ')':
                    currentLevel--;
                    break;
                case '\\':
                    index++;
                    continue;
            }

            if (currentLevel <= 0)
                return true;
        }

        return currentLevel != 0;
    }
    
    /// <summary>
    /// Serializes a collection to a string.  It will separate the items with <c>", "</c> and the last item with <c>", and "</c>.
    /// </summary>
    /// <param name="collection">The collection to serialize.</param>
    /// <returns>The serialized string.</returns>
    public static string Serialize<T>(
        this IEnumerable<T> collection) =>
        collection.Serialize(", and ");
    
    public static string Serialize<T>(
        this IEnumerable<T> collection,
        string lastDelimiter) =>
        collection.Serialize(", ", lastDelimiter);
    
    public static string Serialize<T>(
        this IEnumerable<T> collection,
        string delimiter,
        string lastDelimiter)
    {
        using var enumerator = collection.GetEnumerator();

        var noMoreItems = !enumerator.MoveNext();
        if (noMoreItems)
            return "";
        
        var currentItem = enumerator.Current;
        noMoreItems = !enumerator.MoveNext();
        if (noMoreItems)
            return $"{currentItem}";
        
        var builder = new StringBuilder();
        builder.Append(currentItem);
        do
        {
            currentItem = enumerator.Current;
            noMoreItems = !enumerator.MoveNext();
            var currentDelimiter = noMoreItems
                ? lastDelimiter
                : delimiter;

            builder.Append(currentDelimiter);
            builder.Append(currentItem);
        } while (!noMoreItems);
        
        return builder.ToString();
    }
    
}