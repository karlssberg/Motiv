namespace Motiv;

internal static class PropositionExtensions
{
    internal static string ToReason(
        this string propositionStatement,
        bool isSatisfied)
    {
        return isSatisfied switch
        {
            true => propositionStatement,
            false => propositionStatement.AsUnsatisfied()
        };

    }
}
