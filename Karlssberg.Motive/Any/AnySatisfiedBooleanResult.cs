namespace Karlssberg.Motive.Any;

public sealed class AnySatisfiedBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    internal AnySatisfiedBooleanResult(
        Func<bool, IEnumerable<TMetadata>> metadataFactory,
        IEnumerable<BooleanResultBase<TMetadata>> operandResults)
    {
        OperandResults = operandResults
            .ThrowIfNull(nameof(operandResults))
            .ToArray();
        
        var isSatisfied = OperandResults.Any(result => result.IsSatisfied);

        IsSatisfied = isSatisfied;
        SubstituteMetadata = metadataFactory(isSatisfied);
    }

    public IEnumerable<TMetadata> SubstituteMetadata { get; }
    public IEnumerable<BooleanResultBase<TMetadata>> OperandResults { get; }

    public IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperandResults => OperandResults
        .Where(result => result.IsSatisfied == IsSatisfied);

    public override bool IsSatisfied { get; }

    public override string Description => $"ANY:{IsSatisfiedDisplayText}({string.Join(", ", OperandResults)})";

    public override IEnumerable<string> Reasons =>
        DeterminativeOperandResults.SelectMany(r => r.Reasons);
}