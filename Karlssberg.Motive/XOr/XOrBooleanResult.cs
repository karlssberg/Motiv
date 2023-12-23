namespace Karlssberg.Motive.XOr;

public sealed record XOrBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> LeftOperandResult,
    BooleanResultBase<TMetadata> RightOperandResult) : BooleanResultBase<TMetadata>
{
    public override bool IsSatisfied { get; } = LeftOperandResult.IsSatisfied ^ RightOperandResult.IsSatisfied;
    public BooleanResultBase<TMetadata> LeftOperandResult { get; } = LeftOperandResult ?? throw new ArgumentNullException(nameof(LeftOperandResult));
    public BooleanResultBase<TMetadata> RightOperandResult { get; } = RightOperandResult ?? throw new ArgumentNullException(nameof(RightOperandResult));

    public override void Accept<TVisitor>(TVisitor visitor)
    {
        var action = visitor.Visit(this);
        if (action == VisitorAction.SkipOperands)
            return;
        
        LeftOperandResult.Accept(visitor);
        RightOperandResult.Accept(visitor);
    }
    
    public override string Description => $"({LeftOperandResult}) XOR:{IsSatisfied} ({RightOperandResult})";
    public override string ToString() => Description;
}