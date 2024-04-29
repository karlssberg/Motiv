namespace Motiv;

internal static class Indent
{
    private const string Value = "    ";

    internal static string IndentLine(this string line) =>
        $"{Value}{line}";

    internal static string IndentLine(this string line, int count) => 
        $"{GetIndentations(count)}{line}";

    private static string GetIndentations(int count) => string.Concat(Enumerable.Repeat(Value, count));

    internal static IEnumerable<string> IndentLines(this IEnumerable<string> lines) =>
        lines.Select(line => $"{Value}{line}");
    
    internal static IEnumerable<string> IndentFromLine(this IEnumerable<string> lines, int startLine)
    {
        foreach (var (line, index) in lines.WithIndex())
            yield return index < startLine
                ? line
                : $"{Value}{line}";
    }
}