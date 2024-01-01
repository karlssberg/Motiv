namespace Karlssberg.Motive.AtMost;

public sealed class AtMostBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    internal AtMostBooleanResult(
        int maximum,
        Func<bool, IEnumerable<TMetadata>> metadataFactory,
        IEnumerable<BooleanResultBase<TMetadata>> operandResults)
    {
        var operandResultsArray = operandResults.ThrowIfNull(nameof(operandResults)).ToArray();
        var isSatisfied = operandResultsArray.Count(result => result.IsSatisfied) <= maximum;
        var metadata = metadataFactory(isSatisfied);

        Maximum = maximum;
        IsSatisfied = isSatisfied;
        SubstituteMetadata = metadata;
        OperandResults = operandResultsArray;
        DeterminativeOperandResults = operandResultsArray.Where(result => result.IsSatisfied == isSatisfied);
    }

    public IEnumerable<TMetadata> SubstituteMetadata { get; }

    public IEnumerable<BooleanResultBase<TMetadata>> OperandResults { get; }
    
    public IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperandResults { get; }

    public int Maximum { get; }

    public override bool IsSatisfied { get; }

    public override string Description => $"AT_MOST[{Maximum}]:{IsSatisfiedDisplayText}({string.Join(", ", OperandResults)})";
    
    public override IEnumerable<string> Reasons => 
        DeterminativeOperandResults.SelectMany(r => r.Reasons);
}