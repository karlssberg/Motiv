using static Karlssberg.Motive.VisitorAction;

namespace Karlssberg.Motive.Any;

public sealed record AnyBooleanResult<TMetadata> : BooleanResultBase<TMetadata>
{
    public AnyBooleanResult(Func<bool, IEnumerable<TMetadata>> metadataFactory,
        IEnumerable<BooleanResultBase<TMetadata>> operandResults)
    {;
        var operandResultsArray = Throw.IfNull(operandResults, nameof(operandResults)).ToArray();
        var isSatisfied = operandResultsArray.Any(result => result.IsSatisfied);
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
            .Where(result => result.IsSatisfied == IsSatisfied  || visitorResult == VisitAllOperands)
            .Accept(visitor);
    }
    
    public override string Description => $"ANY:{IsSatisfied}({string.Join(", ", OperandResults)})";
    public override string ToString() => Description;

    public void Deconstruct(
        out IEnumerable<TMetadata> Metadata,
        out IEnumerable<BooleanResultBase<TMetadata>> OperandResults)
    {
        Metadata = this.Metadata;
        OperandResults = this.OperandResults;
    }
}