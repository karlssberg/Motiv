namespace Karlssberg.Motive.Any;

public sealed class AnyBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    
    internal AnyBooleanResult(
        Func<bool, IEnumerable<TMetadata>> metadataFactory,
        IEnumerable<BooleanResultBase<TMetadata>> operandResults)
    {
        var operandResultsArray = operandResults.ThrowIfNull(nameof(operandResults)).ToArray();
        var isSatisfied = operandResultsArray.Any(result => result.IsSatisfied);
        var metadata = metadataFactory(isSatisfied);

        IsSatisfied = isSatisfied;
        SubstituteMetadata = metadata;
        OperandResults = operandResultsArray;
        DeterminativeOperandResults = operandResultsArray.Where(result => result.IsSatisfied == isSatisfied);
    }

    public IEnumerable<TMetadata> SubstituteMetadata { get; }
    public IEnumerable<BooleanResultBase<TMetadata>> OperandResults { get; }
    
    public IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperandResults { get; }

    public override bool IsSatisfied { get; }

    public override string Description => $"ANY:{IsSatisfiedDisplayText}({string.Join(", ", OperandResults)})";
    public override IEnumerable<string> Reasons => 
        DeterminativeOperandResults.SelectMany(r => r.Reasons);
}