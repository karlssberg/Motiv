namespace Karlssberg.Motive.XOr;

public sealed class XOrBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    internal XOrBooleanResult(
        BooleanResultBase<TMetadata> leftOperandResult,
        BooleanResultBase<TMetadata> rightOperandResult)
    {
        IsSatisfied = leftOperandResult.IsSatisfied ^ rightOperandResult.IsSatisfied;
        LeftOperandResult = leftOperandResult ?? throw new ArgumentNullException(nameof(leftOperandResult));
        RightOperandResult = rightOperandResult ?? throw new ArgumentNullException(nameof(rightOperandResult));
    }

    public override bool IsSatisfied { get; }

    public BooleanResultBase<TMetadata> LeftOperandResult { get; }
    public BooleanResultBase<TMetadata> RightOperandResult { get; }


    public BooleanResultBase<TMetadata>[] OperandResults => [LeftOperandResult, RightOperandResult];

    public IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperandResults => OperandResults;

    public override string Description => $"({LeftOperandResult}) XOR:{IsSatisfiedDisplayText} ({RightOperandResult})";

    public override IEnumerable<string> Reasons =>
        DeterminativeOperandResults.SelectMany(r => r.Reasons);
}