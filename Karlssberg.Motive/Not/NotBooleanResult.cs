namespace Karlssberg.Motive.Not;

public sealed class NotBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    internal NotBooleanResult(BooleanResultBase<TMetadata> operandResult)
    {
        OperandResult = Throw.IfNull(operandResult, nameof(operandResult));
        IsSatisfied = !operandResult.IsSatisfied;
    }

    public BooleanResultBase<TMetadata> OperandResult { get; }

    public override bool IsSatisfied { get; }

    public override string Description => $"NOT:{IsSatisfied}({OperandResult})";
    public override IEnumerable<string> Reasons => OperandResult.Reasons;
}