using Motiv.Shared;
using Motiv.Traversal;

namespace Motiv.XOr;

internal sealed class XOrBooleanResultDescription<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right)
    : ResultDescriptionBase
{
    private readonly BooleanResultBase<TMetadata>[] _results = [left, right];

    internal override int CausalOperandCount => _results.Length;

    internal override string Statement => Operator.XOr;

    public override string Reason => FoldedReason;

    // Stryker disable once Assignment : no output or cost change — read only by the reason fold, whose result FoldedReason memoises (#294)
    private protected override IReadOnlyList<ResultDescriptionBase> ReasonOperands =>
        field ??= Array.ConvertAll(_results, result => result.Description);

    private protected override string ComposeReason(IReadOnlyList<string> operandReasons) =>
        string.Join(" ^ ", Explained(operandReasons));

    private IEnumerable<string> Explained(IReadOnlyList<string> operandReasons)
    {
        for (var i = 0; i < _results.Length; i++)
        {
            var reason = operandReasons[i];

            yield return ContainsBinaryOperation(_results[i]) switch
            {
                true => $"({reason})",
                false when reason.EndsWithEqualityAssertion() => $"({reason})",
                false => reason
            };
        }
    }

    public override IEnumerable<string> GetJustificationAsLines() => FoldedJustification(withoutCausalCount: false);

    private protected override IReadOnlyList<Rendering> JustificationOperands(bool withoutCausalCount) =>
        Collapsed.Select(result => new Rendering(result.Description, withoutCausalCount)).ToArray();

    private protected override string[] ComposeJustification(
        IReadOnlyList<string[]> operandLines,
        bool withoutCausalCount) =>
        BinaryJustification(Statement, operandLines);

    // Stryker disable once Assignment : no output change, performance only — read only while rendering the justification, so a rebuild changes its cost, not its lines (#294)
    private IReadOnlyList<BooleanResultBase> Collapsed => field ??= _results.FlattenCollapsible(Statement);

    /// <remarks>Iterative: the result tree it searches is unbounded in depth (Spec 3A / ticket 19).</remarks>
    private static bool ContainsBinaryOperation(BooleanResultBase result)
    {
        var pending = new Stack<BooleanResultBase>();
        pending.Push(result);

        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case IBinaryBooleanOperationResult:
                    return true;
                case IUnaryOperationResult:
                    continue;
                case ExplanationBooleanResult explanation:
                    pending.Push(explanation.Operand);
                    continue;
                case var other:
                    foreach (var underlying in other.Underlying)
                        pending.Push(underlying);
                    continue;
            }
        }

        return false;
    }
}
