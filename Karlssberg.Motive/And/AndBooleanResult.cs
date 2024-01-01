namespace Karlssberg.Motive.And;

public sealed class AndBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    internal AndBooleanResult(
        BooleanResultBase<TMetadata> leftOperandResult,
        BooleanResultBase<TMetadata> rightOperandResult)
    {
        LeftOperandResult = leftOperandResult.ThrowIfNull(nameof(leftOperandResult));
        RightOperandResult = rightOperandResult.ThrowIfNull(nameof(rightOperandResult));

        IsSatisfied = leftOperandResult.IsSatisfied && rightOperandResult.IsSatisfied;
    }

    public BooleanResultBase<TMetadata> LeftOperandResult { get; }
    public BooleanResultBase<TMetadata> RightOperandResult { get; }
    
    public BooleanResultBase<TMetadata>[] OperandResults => [LeftOperandResult, RightOperandResult];

    public IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperandResults => OperandResults
        .Where(r => r.IsSatisfied == IsSatisfied);

    public override bool IsSatisfied { get; }

    public override string Description => $"({LeftOperandResult}) AND:{IsSatisfiedDisplayText} ({RightOperandResult})";

    public override IEnumerable<string> Reasons =>
        DeterminativeOperandResults.SelectMany(r => r.Reasons);
}