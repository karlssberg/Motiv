namespace Karlssberg.Motive.Not;

public sealed record NotBooleanResult<TMetadata>(BooleanResultBase<TMetadata> OperandResult) : BooleanResultBase<TMetadata>
{
    public BooleanResultBase<TMetadata> OperandResult { get; } = Throw.IfNull(OperandResult, nameof(OperandResult));

    public override bool IsSatisfied { get; } = !OperandResult.IsSatisfied;

    public override void Accept<TVisitor>(TVisitor visitor)
    {
        var acton = visitor.Visit(this);
        if (acton == VisitorAction.SkipOperands)
            return;
        
        OperandResult.Accept(visitor);
    }
    
    public override string Description => $"NOT:{IsSatisfied}({OperandResult})";
    public override string ToString() => Description;
}