namespace Motiv.Shared;

internal static class IndentStringExtensions
{
    private const string Value = "    ";

    private static readonly string[] IndentCache =
        Enumerable.Range(0, 9).Select(i => string.Concat(Enumerable.Repeat(Value, i))).ToArray();

    internal static string Indent(this string line, int levelOfIndentation = 1) =>
        levelOfIndentation < IndentCache.Length
            ? IndentCache[levelOfIndentation] + line
            : string.Concat(Enumerable.Repeat(Value, levelOfIndentation)) + line;
}
