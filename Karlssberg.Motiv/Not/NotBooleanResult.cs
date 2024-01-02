namespace Karlssberg.Motiv.Not;

public sealed class NotBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    internal NotBooleanResult(BooleanResultBase<TMetadata> operandResult)
    {
        OperandResult = operandResult.ThrowIfNull(nameof(operandResult));
        IsSatisfied = !operandResult.IsSatisfied;
    }

    public BooleanResultBase<TMetadata> OperandResult { get; }

    public override bool IsSatisfied { get; }

    public override string Description => $"NOT:{IsSatisfiedDisplayText}({OperandResult})";
    public override IEnumerable<string> Reasons => OperandResult.Reasons;
}