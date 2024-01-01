namespace Karlssberg.Motive.AtLeast;

public sealed class AtLeastNSatisfiedBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    internal AtLeastNSatisfiedBooleanResult(
        int minimum,
        Func<bool, IEnumerable<TMetadata>> metadataFactory,
        IEnumerable<BooleanResultBase<TMetadata>> operandResults)
    {
        OperandResults = operandResults
            .ThrowIfNull(nameof(operandResults))
            .ToArray();
        
        var isSatisfied = OperandResults.Count(result => result.IsSatisfied) >= minimum;

        Minimum = minimum;
        IsSatisfied = isSatisfied;
        SubstituteMetadata = metadataFactory(isSatisfied);
    }

    public IEnumerable<TMetadata> SubstituteMetadata { get; }

    public IEnumerable<BooleanResultBase<TMetadata>> OperandResults { get; }
    
    public IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperandResults => OperandResults
        .Where(result => result.IsSatisfied == IsSatisfied);

    public int Minimum { get; }

    public override bool IsSatisfied { get; }

    public override string Description => $"AT_LEAST_{Minimum}:{IsSatisfiedDisplayText}({string.Join(", ", OperandResults)})";
    public override IEnumerable<string> Reasons => 
        DeterminativeOperandResults.SelectMany(r => r.Reasons);
}