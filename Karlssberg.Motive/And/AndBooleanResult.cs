using static Karlssberg.Motive.VisitorAction;

namespace Karlssberg.Motive.And;

public sealed record AndBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> LeftOperandResult, 
    BooleanResultBase<TMetadata> RightOperandResult) : BooleanResultBase<TMetadata>
{
    public BooleanResultBase<TMetadata> LeftOperandResult { get; } = Throw.IfNull(LeftOperandResult, nameof(LeftOperandResult));
    public BooleanResultBase<TMetadata> RightOperandResult { get; } = Throw.IfNull(RightOperandResult, nameof(RightOperandResult));

    public override bool IsSatisfied { get; } = LeftOperandResult.IsSatisfied && RightOperandResult.IsSatisfied;

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
    
    public override string Description => $"({LeftOperandResult}) AND:{IsSatisfied} ({RightOperandResult})";
    public override string ToString() => Description;
}