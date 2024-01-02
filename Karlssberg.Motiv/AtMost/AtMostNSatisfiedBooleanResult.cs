namespace Karlssberg.Motiv.AtMost;

public sealed class AtMostNSatisfiedBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    internal AtMostNSatisfiedBooleanResult(
        int maximum,
        Func<bool, IEnumerable<TMetadata>> metadataFactory,
        IEnumerable<BooleanResultBase<TMetadata>> operandResults)
    {
        OperandResults = operandResults
            .ThrowIfNull(nameof(operandResults))
            .ToArray();

        var isSatisfied = OperandResults.Count(result => result.IsSatisfied) <= maximum;

        Maximum = maximum;
        IsSatisfied = isSatisfied;
        SubstituteMetadata = metadataFactory(isSatisfied);
    }

    public IEnumerable<TMetadata> SubstituteMetadata { get; }

    public IEnumerable<BooleanResultBase<TMetadata>> OperandResults { get; }

    public IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperandResults => OperandResults
        .Where(result => result.IsSatisfied == IsSatisfied);

    public int Maximum { get; }

    public override bool IsSatisfied { get; }

    public override string Description => $"AT_MOST_{Maximum}:{IsSatisfiedDisplayText}({string.Join(", ", OperandResults)})";

    public override IEnumerable<string> Reasons =>
        DeterminativeOperandResults.SelectMany(r => r.Reasons);
}