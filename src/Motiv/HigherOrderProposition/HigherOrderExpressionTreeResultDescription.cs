using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition;

internal sealed class HigherOrderExpressionTreeResultDescription<TUnderlyingMetadata>(
    bool satisfied,
    string reason,
    LambdaExpression expression,
    IEnumerable<BooleanResultBase<TUnderlyingMetadata>> causes,
    string propositionStatement)
    : HigherOrderResultDescriptionBase<TUnderlyingMetadata>(reason, causes, propositionStatement)
{
    public override IEnumerable<string> GetJustificationAsLines()
    {
        yield return Reason;

        yield return $"{expression.ToAssertion(satisfied)} ({CausalOperandCount})".Indent();

        foreach (var line in GetUnderlyingJustificationsAsLines())
        {
            yield return line.Indent(2);
        }
    }

    internal override IEnumerable<string> GetJustificationAsLinesWithoutCausalCount()
    {
        yield return Reason;

        yield return expression.ToAssertion(satisfied).Indent();

        foreach (var line in GetUnderlyingJustificationsAsLines())
        {
            yield return line.Indent(2);
        }
    }
}
