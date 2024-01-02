namespace Karlssberg.Motiv;

public static class BooleanResultBaseExtensions
{
    public static IEnumerable<TMetadata> GetInsights<TMetadata>(this BooleanResultBase<TMetadata> booleanResultBase) =>
        booleanResultBase
            .GetInsights(new DefaultInsightsVisitor<TMetadata>())
            .Distinct();

    public static IEnumerable<TMetadata> GetInsights<TMetadata, TVisitor>(
        this BooleanResultBase<TMetadata> booleanResultBase,
        TVisitor visitor)
        where TVisitor : DefaultInsightsVisitor<TMetadata> =>
        visitor.Visit(booleanResultBase); 
}