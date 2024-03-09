using Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders;

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

    internal static string ToReason<TMetadata>(
        this IProposition proposition,
        bool isSatisfied,
        TMetadata metadata,
        AssertionSource assertionSource = AssertionSource.Unknown)
    {
        return (isSatisfied, metadata, assertionSource) switch
        {
            (_, string reason, not AssertionSource.Proposition) => reason,
            (true, _, _) when PropositionContains('!') => $"({proposition.Assertion})",
            (true, _, _) => proposition.Assertion,
            (false, _, _) when PropositionContains('!') => $"!({proposition.Assertion})",
            (false, _, _) => $"!{proposition.Assertion}"
        };

        bool PropositionContains(char ch) => proposition.Assertion.Contains(ch);
    }
    
    internal static string ToReason(
        this IProposition proposition,
        bool isSatisfied)
    {
        return isSatisfied switch
        {
            true when PropositionContains('!') => $"({proposition.Assertion})",
            true => proposition.Assertion,
            false when PropositionContains('!') => $"!({proposition.Assertion})",
            false => $"!{proposition.Assertion}"
        };

        bool PropositionContains(char ch) => proposition.Assertion.Contains(ch);
    }
}