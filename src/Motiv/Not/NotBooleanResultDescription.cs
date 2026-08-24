using Motiv.Traversal;

namespace Motiv.Not;

internal sealed class NotBooleanResultDescription<TMetadata>(BooleanResultBase operand) : ResultDescriptionBase
{
    internal override int CausalOperandCount => 1;

    internal override string Statement => Operator.Not;

    public override string Reason => FoldedReason;

    private protected override IReadOnlyList<ResultDescriptionBase> ReasonOperands => field ??= [operand.Description];

    private protected override string ComposeReason(IReadOnlyList<string> operandReasons) =>
        FormatReason(operand, operandReasons[0]);

    public override IEnumerable<string> GetJustificationAsLines() => FoldedJustification(withoutCausalCount: false);

    internal override IEnumerable<string> GetJustificationAsLinesWithoutCausalCount() =>
        FoldedJustification(withoutCausalCount: true);

    private protected override IReadOnlyList<Rendering> JustificationOperands(bool withoutCausalCount) =>
        [new Rendering(operand.Description, withoutCausalCount)];

    private protected override string[] ComposeJustification(
        IReadOnlyList<string[]> operandLines,
        bool withoutCausalCount) =>
        NegateFirstLine(operandLines[0]).ToArray();

    private static IEnumerable<string> NegateFirstLine(IEnumerable<string> lines) =>
        lines.ReplaceFirstLine(firstLine =>
            JustificationNegationMappings.Instance.TryGetValue(firstLine, out var negated)
                ? negated
                : firstLine);

    private static string FormatReason(BooleanResultBase result, string reason)
    {
        return result switch
        {
            NotPolicyResult<TMetadata> notResult => NegateNotOperator(notResult),
            NotBooleanOperationResult<TMetadata> notResult => NegateNotOperator(notResult),
            IBooleanOperationResult =>  $"!({reason})",
            _ when reason.EndsWithEqualityAssertion() => $"!({reason})",
            _ =>$"!{reason}"
        };
    }

    private static string NegateNotOperator(IUnaryOperationResult<TMetadata> notOperationResult)
    {
        var count = 0;
        var current = notOperationResult;
        while (current.Operand is IUnaryOperationResult<TMetadata> nested)
        {
            count++;
            current = nested;
        }

        return (count % 2 == 0, current.Operand) switch
        {
            (true, _) => current.Operand.Reason,
            (false, IBooleanOperationResult) => $"!({current.Operand.Reason})",
            (false, _) when current.Operand.Reason.EndsWithEqualityAssertion() => $"!({current.Operand.Reason})",
            (false, _) => $"!{current.Operand.Reason}",
        };
    }
}
