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
    // Stryker disable once Assignment : equivalent — read only while rendering the justification, so a rebuild changes its cost, not its lines (#294)
    private string Assertion => field ??= expression.ToAssertion(satisfied);

    public override IEnumerable<string> GetJustificationAsLines() => FoldedJustification(withoutCausalCount: false);

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
