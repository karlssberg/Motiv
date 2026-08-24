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
    private string Assertion => field ??= expression.ToAssertion(satisfied);

    public override IEnumerable<string> GetJustificationAsLines() => FoldedJustification(withoutCausalCount: false);

    internal override IEnumerable<string> GetJustificationAsLinesWithoutCausalCount() =>
        FoldedJustification(withoutCausalCount: true);

    private protected override string[] ComposeJustification(
        IReadOnlyList<string[]> operandLines,
        bool withoutCausalCount) =>
        Render(operandLines, withoutCausalCount).ToArray();

    private IEnumerable<string> Render(IReadOnlyList<string[]> causeLines, bool withoutCausalCount)
    {
        yield return Reason;

        yield return withoutCausalCount
            ? Assertion.Indent()
            : $"{Assertion} ({CausalOperandCount})".Indent();

        foreach (var line in UnderlyingJustifications(causeLines))
        {
            yield return line.Indent(2);
        }
    }
}
