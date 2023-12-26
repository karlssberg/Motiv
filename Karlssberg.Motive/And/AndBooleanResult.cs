namespace Karlssberg.Motive.And;

public sealed class AndBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    internal AndBooleanResult(
        BooleanResultBase<TMetadata> leftOperandResult,
        BooleanResultBase<TMetadata> rightOperandResult)
    {
        LeftOperandResult = Throw.IfNull(leftOperandResult, nameof(leftOperandResult));
        RightOperandResult = Throw.IfNull(rightOperandResult, nameof(rightOperandResult));
        
        IsSatisfied = leftOperandResult.IsSatisfied && rightOperandResult.IsSatisfied;
        OperandResults = [leftOperandResult, rightOperandResult];
        DeterminativeOperandResults = OperandResults
            .Where(r => r.IsSatisfied == IsSatisfied);
    }

    public BooleanResultBase<TMetadata>[] OperandResults { get; }
    public BooleanResultBase<TMetadata> LeftOperandResult { get; }
    public BooleanResultBase<TMetadata> RightOperandResult { get; }
    public IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperandResults { get; }

    public override bool IsSatisfied { get; }

    public override string Description => $"({LeftOperandResult}) AND:{IsSatisfied} ({RightOperandResult})";
    public override IEnumerable<string> Reasons =>
        DeterminativeOperandResults.SelectMany(r => r.Reasons);
}