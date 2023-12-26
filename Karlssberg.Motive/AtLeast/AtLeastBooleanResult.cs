namespace Karlssberg.Motive.AtLeast;

public sealed class AtLeastBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    internal AtLeastBooleanResult(
        int minimum,
        Func<bool, IEnumerable<TMetadata>> metadataFactory,
        IEnumerable<BooleanResultBase<TMetadata>> operandResults)
    {
        var operandResultsArray = Throw.IfNull(operandResults, nameof(operandResults)).ToArray();
        var isSatisfied = operandResultsArray.Count(result => result.IsSatisfied) >= minimum;
        var metadata = metadataFactory(isSatisfied);

        Minimum = minimum;
        IsSatisfied = isSatisfied;
        SubstituteMetadata = metadata;
        OperandResults = operandResultsArray;
        DeterminativeOperandResults = operandResultsArray.Where(result => result.IsSatisfied == isSatisfied);
    }

    public IEnumerable<TMetadata> SubstituteMetadata { get; }

    public IEnumerable<BooleanResultBase<TMetadata>> OperandResults { get; }
    
    public IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperandResults { get; }

    public int Minimum { get; }

    public override bool IsSatisfied { get; }

    public override string Description => $"AT_LEAST[{Minimum}]:{IsSatisfied}({string.Join(", ", OperandResults)})";
    public override IEnumerable<string> Reasons => 
        DeterminativeOperandResults.SelectMany(r => r.Reasons);
}