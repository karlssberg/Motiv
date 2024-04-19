namespace Karlssberg.Motiv;

internal static class BooleanResultExtensions
{

    internal static IEnumerable<MetadataTree<TMetadata>> ResolveMetadataTrees<TMetadata, TUnderlyingMetadata>(
        this BooleanResultBase<TUnderlyingMetadata> underlyingResult) =>
        underlyingResult switch
        {
            BooleanResultBase<TMetadata> result => result.MetadataTree.ToEnumerable(),
            _ => Enumerable.Empty<MetadataTree<TMetadata>>()
        };

    internal static IEnumerable<BooleanResultBase<TMetadata>> ResolveCausesWithMetadata<TMetadata, TUnderlyingMetadata>(
        this BooleanResultBase<TUnderlyingMetadata> booleanResult) =>
        booleanResult.Causes switch
        {
            IEnumerable<BooleanResultBase<TMetadata>> results => results,
            _ => Enumerable.Empty<BooleanResultBase<TMetadata>>()
        };
    
    internal static IEnumerable<BooleanResultBase<TMetadata>> ResolveCausesWithMetadata<TMetadata, TUnderlyingMetadata>(
        this IEnumerable<BooleanResultBase<TUnderlyingMetadata>> booleanResults) =>
        booleanResults switch
        {
            IEnumerable<BooleanResultBase<TMetadata>> results => results,
            _ => Enumerable.Empty<BooleanResultBase<TMetadata>>()
        };
    
    internal static IEnumerable<BooleanResultBase<TMetadata>> ResolveUnderlyingWithMetadata<TMetadata, TUnderlyingMetadata>(
        this BooleanResultBase<TUnderlyingMetadata> booleanResult) =>
        booleanResult switch
        {
            BooleanResultBase<TMetadata> result=> result.ToEnumerable(),
            _ => Enumerable.Empty<BooleanResultBase<TMetadata>>()
        };
    
    internal static IEnumerable<BooleanResultBase<TMetadata>> ResolveUnderlyingWithMetadata<TMetadata, TUnderlyingMetadata>(
        this IEnumerable<BooleanResultBase<TUnderlyingMetadata>> booleanResults) =>
        booleanResults switch
        {
            IEnumerable<BooleanResultBase<TMetadata>> results => results,
            _ => Enumerable.Empty<BooleanResultBase<TMetadata>>()
        };

    internal static IEnumerable<Explanation> FindUnderlyingExplanations(this BooleanResultBase booleanResult) =>
        booleanResult switch
        {
            IBooleanOperationResult operationBooleanResult =>
                operationBooleanResult.UnderlyingAssertionSources.GetExplanations(),
            _ => booleanResult.Explanation.ToEnumerable()
        };
}