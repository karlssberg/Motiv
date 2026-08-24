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

    public override IEnumerable<string> GetJustificationAsLines() => FoldedJustification(withoutCausalCount: false);

    /// <remarks>
    /// The underlying lines are always taken without the causal count, in both modes — this
    /// description supplies its own heading for the expression.
    /// </remarks>
    private protected override IReadOnlyList<Rendering> JustificationOperands(bool withoutCausalCount) =>
        [new Rendering(BooleanResult.Description, true)];

    private protected override string[] ComposeJustification(
        IReadOnlyList<string[]> operandLines,
        bool withoutCausalCount) =>
        Render(operandLines[0]).ToArray();

    private IEnumerable<string> Render(IReadOnlyList<string> underlyingLines)
    {
        if (IsReasonTheSameAsUnderlying())
        {
            yield return Assertion;
            foreach (var line in underlyingLines)
                yield return line.Indent();

            yield break;
        }

        yield return Reason;
        yield return Assertion.Indent();
        foreach (var line in underlyingLines)
            yield return line.Indent(2);
    }
}
