using Karlssberg.Motive.And;
using Karlssberg.Motive.Not;
using Karlssberg.Motive.Or;
using Karlssberg.Motive.XOr;

namespace Karlssberg.Motive;

public abstract record BooleanResultBase<TMetadata>
{
    public abstract bool IsSatisfied { get; }
    
    public abstract string Description { get; }

    public abstract void Accept<TVisitor>(TVisitor visitor)
        where TVisitor : IBooleanResultVisitor<TMetadata>;

    public BooleanResultBase<TMetadata> And(BooleanResultBase<TMetadata> otherResult) =>
        new AndBooleanResult<TMetadata>(this, otherResult);

    public BooleanResultBase<TMetadata> Or(BooleanResultBase<TMetadata> otherResult) =>
        new OrBooleanResult<TMetadata>(this, otherResult);
    
    public BooleanResultBase<TMetadata> XOr(BooleanResultBase<TMetadata> otherResult) =>
        new XOrBooleanResult<TMetadata>(this, otherResult);
    
    public BooleanResultBase<TMetadata> Not() =>
        new NotBooleanResult<TMetadata>(this);
    
    public IEnumerable<TMetadata> Causes => FindRootCauses();

    private IEnumerable<TMetadata> FindRootCauses()
    {
        var rootCauseVisitor = new CausesVisitor<TMetadata>();
        Accept(rootCauseVisitor);
        return rootCauseVisitor.RootCauses;
    }

    public override string ToString() => Description;

    public static BooleanResultBase<TMetadata> operator &(
        BooleanResultBase<TMetadata> leftResult,
        BooleanResultBase<TMetadata> rightResult) =>
        leftResult.And(rightResult);

    public static BooleanResultBase<TMetadata> operator |(
        BooleanResultBase<TMetadata> leftResult,
        BooleanResultBase<TMetadata> rightResult) =>
        leftResult.Or(rightResult);
    
    public static BooleanResultBase<TMetadata> operator ^(
        BooleanResultBase<TMetadata> leftResult,
        BooleanResultBase<TMetadata> rightResult) =>
        leftResult.XOr(rightResult);

    public static BooleanResultBase<TMetadata> operator !(
        BooleanResultBase<TMetadata> result) =>
        result.Not();
}