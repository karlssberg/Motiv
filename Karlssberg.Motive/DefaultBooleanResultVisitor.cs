using Karlssberg.Motive.All;
using Karlssberg.Motive.And;
using Karlssberg.Motive.Any;
using Karlssberg.Motive.Not;
using Karlssberg.Motive.Or;
using Karlssberg.Motive.XOr;

namespace Karlssberg.Motive;

public class DefaultBooleanResultVisitor<TMetadata> : IBooleanResultVisitor<TMetadata>
{
    public virtual VisitorAction Visit(AnyBooleanResult<TMetadata> anyBooleanResult) => default;
    public virtual VisitorAction Visit(AllBooleanResult<TMetadata> allBooleanResult) => default;
    public virtual VisitorAction Visit(AndBooleanResult<TMetadata> andBooleanResult) => default;
    public virtual VisitorAction Visit(OrBooleanResult<TMetadata> orBooleanResult) => default;
    public virtual VisitorAction Visit(XOrBooleanResult<TMetadata> xOrBooleanResult) => default;
    public virtual VisitorAction Visit(NotBooleanResult<TMetadata> notBooleanResult) => default;
    public virtual VisitorAction Visit(BooleanResult<TMetadata> booleanResult) => default;
}