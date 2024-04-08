namespace Karlssberg.Motiv;

internal static class PropositionExtensions
{
    private const int Spaces = 4;

    internal static string IndentAfterFirstLine(this string text, int levels = 1)
    {
        var indentation = string.Join("", Enumerable.Repeat(" ", Spaces * levels));
        return text.Replace("\n", $"\n{indentation}");
    }

    internal static string Indent(this string text, int levels = 1)
    {
        var indentation = string.Join("", Enumerable.Repeat(" ", Spaces * levels));
        return $"{indentation}{text.Replace("\n", $"\n{indentation}")}";
    }

    internal static string JoinLines(this IEnumerable<string> textCollection) =>
        string.Join(Environment.NewLine, textCollection);


    
    internal static string ToReason(
        this IProposition proposition,
        bool isSatisfied) =>
        proposition.Statement.ToReason(isSatisfied);

    internal static string ToReason(
        this string propositionStatement,
        bool isSatisfied)
    {
        return isSatisfied switch
        {
            true when PropositionContains('!') => $"({propositionStatement})",
            true => propositionStatement,
            false when PropositionContains('!') => $"!({propositionStatement})",
            false => $"!{propositionStatement}"
        };

        bool PropositionContains(char ch) => propositionStatement.Contains(ch);
    }
}