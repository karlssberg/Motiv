using Karlssberg.Motive.All;
using Karlssberg.Motive.And;
using Karlssberg.Motive.Any;
using Karlssberg.Motive.Not;
using Karlssberg.Motive.Or;
using Karlssberg.Motive.XOr;

namespace Karlssberg.Motive;

public interface IBooleanResultVisitor<TMetadata>
{
    VisitorAction Visit(AnyBooleanResult<TMetadata> anyBooleanResult);
    VisitorAction Visit(AllBooleanResult<TMetadata> allBooleanResult);
    VisitorAction Visit(AndBooleanResult<TMetadata> andBooleanResult);
    VisitorAction Visit(OrBooleanResult<TMetadata> orBooleanResult);
    VisitorAction Visit(XOrBooleanResult<TMetadata> xOrBooleanResult);
    VisitorAction Visit(NotBooleanResult<TMetadata> notBooleanResult);
    VisitorAction Visit(BooleanResult<TMetadata> booleanResult);
}