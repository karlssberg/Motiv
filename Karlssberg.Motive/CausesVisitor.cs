using Karlssberg.Motive.All;
using Karlssberg.Motive.Any;
using static Karlssberg.Motive.VisitorAction;

namespace Karlssberg.Motive;

public class CausesVisitor<TMetadata> : DefaultBooleanResultVisitor<TMetadata>
{
    public IEnumerable<TMetadata> RootCauses => _rootCauses;
    
    private readonly HashSet<TMetadata> _rootCauses = [];
    
    public override VisitorAction Visit(BooleanResult<TMetadata> booleanResult)
    {
        _rootCauses.Add(booleanResult.Metadata);
        return SkipOperands;
    }

    public override VisitorAction Visit(AnyBooleanResult<TMetadata> anyBooleanResult)
    {
        _rootCauses.UnionWith(anyBooleanResult.Metadata);
        return anyBooleanResult.Metadata.Any() 
            ? SkipOperands 
            : VisitDeterminativeOperands;
    }

    public override VisitorAction Visit(AllBooleanResult<TMetadata> allBooleanResult)
    {
        _rootCauses.UnionWith(allBooleanResult.Metadata);
        return allBooleanResult.Metadata.Any() 
            ? SkipOperands 
            : VisitDeterminativeOperands;;
    }
}
