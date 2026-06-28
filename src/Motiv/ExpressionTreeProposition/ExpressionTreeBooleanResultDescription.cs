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
    public override IEnumerable<string> GetJustificationAsLines()
    {
        if (IsReasonTheSameAsUnderlying())
        {
            yield return expression.ToAssertion(BooleanResult.Satisfied);
            foreach (var line in BooleanResult.Description.GetJustificationAsLinesWithoutCausalCount())
                yield return line.Indent();

            yield break;
        }

        yield return Reason;
        yield return expression.ToAssertion(BooleanResult.Satisfied).Indent();
        foreach (var line in BooleanResult.Description.GetJustificationAsLinesWithoutCausalCount())
            yield return line.Indent(2);
    }
}
