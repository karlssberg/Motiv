namespace Karlssberg.Motive.All;

public sealed class AllSatisfiedBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    internal AllSatisfiedBooleanResult(
        Func<bool, IEnumerable<TMetadata>> metadataFactory,
        IEnumerable<BooleanResultBase<TMetadata>> operandResults)
    {
        OperandResults = operandResults
            .ThrowIfNull(nameof(operandResults))
            .ToArray();

        var isSatisfied = OperandResults.All(result => result.IsSatisfied);

        IsSatisfied = isSatisfied;
        SubstituteMetadata = metadataFactory(isSatisfied);
    }

    public IEnumerable<TMetadata> SubstituteMetadata { get; }

    public IEnumerable<BooleanResultBase<TMetadata>> OperandResults { get; }

    public IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperandResults => OperandResults
        .Where(result => result.IsSatisfied == IsSatisfied);

    public override bool IsSatisfied { get; }

    public override string Description => $"ALL:{IsSatisfiedDisplayText}({string.Join(", ", OperandResults)})";

    public override IEnumerable<string> Reasons =>
        DeterminativeOperandResults.SelectMany(r => r.Reasons);
}