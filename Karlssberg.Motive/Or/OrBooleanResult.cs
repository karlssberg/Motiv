using static Karlssberg.Motive.VisitorAction;

namespace Karlssberg.Motive.Or;

public sealed record OrBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> LeftOperandResult,
    BooleanResultBase<TMetadata> RightOperandResult) : BooleanResultBase<TMetadata>
{
    public BooleanResultBase<TMetadata> LeftOperandResult { get; } = LeftOperandResult;
    public BooleanResultBase<TMetadata> RightOperandResult { get; } = RightOperandResult;

    public override bool IsSatisfied { get; } = LeftOperandResult.IsSatisfied || RightOperandResult.IsSatisfied;

    public override void Accept<TVisitor>(TVisitor visitor)
    {
        var visitorResult = visitor.Visit(this);
        if (visitorResult == SkipOperands)
            return;
        
        if (LeftOperandResult.IsSatisfied == IsSatisfied || visitorResult == VisitAllOperands) 
            LeftOperandResult.Accept(visitor);
        
        if (RightOperandResult.IsSatisfied == IsSatisfied || visitorResult == VisitAllOperands) 
            RightOperandResult.Accept(visitor);
    }
    
    public override string Description => $"({LeftOperandResult}) OR:{IsSatisfied} ({RightOperandResult})";
    public override string ToString() => Description;
}