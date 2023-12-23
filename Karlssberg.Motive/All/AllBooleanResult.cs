using static Karlssberg.Motive.VisitorAction;

namespace Karlssberg.Motive.All;

public sealed record AllBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    public AllBooleanResult(Func<bool, IEnumerable<TMetadata>> metadataFactory,
        IEnumerable<BooleanResultBase<TMetadata>> operandResults)
    {
        var operandResultsArray = Throw.IfNull(operandResults, nameof(operandResults)).ToArray();
        var isSatisfied = operandResultsArray.All(result => result.IsSatisfied);
        var metadata = metadataFactory(isSatisfied);
        
        IsSatisfied = isSatisfied;
        Metadata = metadata;
        OperandResults = operandResultsArray;
    }

    public IEnumerable<TMetadata> Metadata { get; }

    public IEnumerable<BooleanResultBase<TMetadata>> OperandResults { get; }

    public override bool IsSatisfied { get; }
    
    public override void Accept<TVisitor>(TVisitor visitor)
    {
        var visitorResult = visitor.Visit(this);
        if (visitorResult == SkipOperands)
            return;
        
        OperandResults
            .Where(result => result.IsSatisfied == IsSatisfied || visitorResult == VisitAllOperands)
            .Accept(visitor);
    }
    
    public override string Description => $"ALL:{IsSatisfied}({string.Join(", ", OperandResults)})";
    public override string ToString() => Description;
}