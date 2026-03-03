using Motiv.Not;
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

    public override string Reason => string.Join(" ^ ", _results.Select(result =>
        ContainsBinaryOperation(result) switch
        {
            true => $"({result.Description.Reason})",
            false when result.Description.Reason.EndsWithEqualityAssertion() => $"({result.Description.Reason})",
            false => result.Description.Reason
        }));

    public override IEnumerable<string> GetJustificationAsLines() =>
        _results.GetBinaryJustificationAsLines(Statement);

    internal override IEnumerable<string> GetJustificationAsLinesWithoutCausalCount() =>
        _results.GetBinaryJustificationAsLines(Statement, withoutCausalCount: true);

    private static bool ContainsBinaryOperation(BooleanResultBase result) =>
        result switch
        {
            IBinaryBooleanOperationResult => true,
            NotBooleanOperationResult<TMetadata> => false,
            _ => result.Underlying.Any(ContainsBinaryOperation)
        };
}
