namespace Karlssberg.Motiv;

internal static class PropositionExtensions
{
    internal static string ToReason(
        this ISpecDescription specDescription,
        bool isSatisfied) =>
        specDescription.Statement.ToReason(isSatisfied);

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