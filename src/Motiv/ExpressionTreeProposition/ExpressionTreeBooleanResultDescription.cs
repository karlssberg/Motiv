using System.Linq.Expressions;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition;

internal sealed class ExpressionTreeBooleanResultDescription(
    BooleanResultBase booleanResult,
    string reason,
    LambdaExpression expression,
    string propositionalStatement)
    : ResultDescriptionWithUnderlying(booleanResult, reason, propositionalStatement)
{
    private string Assertion => field ??= expression.ToAssertion(BooleanResult.Satisfied);

    public override IEnumerable<string> GetJustificationAsLines()
    {
        if (IsReasonTheSameAsUnderlying())
        {
            yield return Assertion;
            foreach (var line in BooleanResult.Description.GetJustificationAsLinesWithoutCausalCount())
                yield return line.Indent();

            yield break;
        }

        yield return Reason;
        yield return Assertion.Indent();
        foreach (var line in BooleanResult.Description.GetJustificationAsLinesWithoutCausalCount())
            yield return line.Indent(2);
    }
}
