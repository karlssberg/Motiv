namespace Karlssberg.Motive.All;

public sealed class AllBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    internal AllBooleanResult(
        Func<bool, IEnumerable<TMetadata>> metadataFactory,
        IEnumerable<BooleanResultBase<TMetadata>> operandResults)
    {
        Throw.IfNull(operandResults, nameof(operandResults));

        OperandResults = operandResults.ToArray();
        var isSatisfied = OperandResults.All(result => result.IsSatisfied);
        var metadata = metadataFactory(isSatisfied);

        IsSatisfied = isSatisfied;
        SubstituteMetadata = metadata;
        DeterminativeOperandResults = OperandResults.Where(result => result.IsSatisfied == isSatisfied);
    }

    public IEnumerable<TMetadata> SubstituteMetadata { get; }

    public IEnumerable<BooleanResultBase<TMetadata>> OperandResults { get; }
    
    public IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperandResults { get; }

    public override bool IsSatisfied { get; }

    public override string Description => $"ALL:{IsSatisfied}({string.Join(", ", OperandResults)})";
    public override IEnumerable<string> Reasons => 
        DeterminativeOperandResults.SelectMany(r => r.Reasons);
}