using Motiv.Not;

namespace Motiv.XOr;

internal sealed class XOrBooleanResultDescription<TMetadata>(
    string operationName,
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right)
    : ResultDescriptionBase
{

    internal override int CausalOperandCount => Results.Count();
    internal override string Statement => "XOR";

    public override string Reason => string.Join(" ^ ", Results.Select(result =>
        ContainsBinaryOperation(result) switch
        {
            true => $"({result.Description.Reason})",
            false => result.Description.Reason
        }));

    public override IEnumerable<string> GetJustificationAsLines()
    {
        var reversedResults = left.ToEnumerable().Append(right); // reverse order for easier reading
        return reversedResults.GetBinaryJustificationAsLines(operationName);
    }

    public override string ToString() => Reason;

    private IEnumerable<BooleanResultBase<TMetadata>> Results =>
        left.ToEnumerable()
            .Append(right);

    private static bool ContainsBinaryOperation(BooleanResultBase result) =>
        result switch
        {
            IBinaryBooleanOperationResult => true,
            NotBooleanOperationResult<TMetadata> => false,
            _ => result.Underlying.Any(ContainsBinaryOperation)
        };
}
