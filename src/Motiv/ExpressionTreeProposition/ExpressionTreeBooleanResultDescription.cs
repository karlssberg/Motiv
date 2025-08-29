using System.Linq.Expressions;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition;

internal sealed class ExpressionTreeBooleanResultDescription(
    BooleanResultBase booleanResult,
    string reason,
    LambdaExpression expression,
    string propositionalStatement)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 1;

    internal override string Statement => propositionalStatement;

    public override string Reason => reason;


    public override IEnumerable<string> GetJustificationAsLines()
    {
        if (IsReasonTheSameAsUnderlying())
        {
            yield return expression.ToAssertion(booleanResult.Satisfied);
            foreach (var line in booleanResult.Description.GetJustificationAsLines())
                yield return line.Indent();

            yield break;
        }

        yield return reason;
        yield return expression.ToAssertion(booleanResult.Satisfied).Indent();
        foreach (var line in booleanResult.Description.GetJustificationAsLines())
            yield return line.Indent().Indent();
    }

    private bool IsReasonTheSameAsUnderlying() => reason == booleanResult.Description.Reason;
}

