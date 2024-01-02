namespace Karlssberg.Motiv.Or;

public sealed class OrBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    internal OrBooleanResult(
        BooleanResultBase<TMetadata> leftOperandResult,
        BooleanResultBase<TMetadata> rightOperandResult)
    {
        LeftOperandResult = leftOperandResult;
        RightOperandResult = rightOperandResult;
        IsSatisfied = leftOperandResult.IsSatisfied || rightOperandResult.IsSatisfied;
    }

    public BooleanResultBase<TMetadata> LeftOperandResult { get; }
    public BooleanResultBase<TMetadata> RightOperandResult { get; }

    public BooleanResultBase<TMetadata>[] OperandResults => [LeftOperandResult, RightOperandResult];

    public IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperandResults => OperandResults
        .Where(r => r.IsSatisfied == IsSatisfied);

    public override bool IsSatisfied { get; }

    public override string Description => $"({LeftOperandResult}) OR:{IsSatisfiedDisplayText} ({RightOperandResult})";

    public override IEnumerable<string> Reasons =>
        DeterminativeOperandResults.SelectMany(r => r.Reasons);
}