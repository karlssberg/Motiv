namespace Motiv.XOr;

internal sealed class XOrBooleanResultDescription<TMetadata>(
    string operationName,
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right) 
    : ResultDescriptionBase
{
    
    internal override int CausalOperandCount => Results.Count();

    public override string Reason => string.Join(" ^ ", Results.Select(result => result.Description.Reason));

    public override IEnumerable<string> GetDetailsAsLines()
    {
        var reversedResults = left.ToEnumerable().Append(right); // reverse order for easier reading
        return reversedResults.GetBinaryJustificationAsLines(operationName);
    }

    public override string ToString() => Reason;
    
    private IEnumerable<BooleanResultBase<TMetadata>> Results => 
        left.ToEnumerable()
            .Append(right);
}