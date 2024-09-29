namespace Motiv.Shared;

internal static class IndentStringExtensions
{
    private const string Value = "    ";

    internal static string Indent(this string line) =>
        $"{Value}{line}";
}
