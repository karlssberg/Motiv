namespace Karlssberg.Motiv.HigherOrderSpecBuilder;

internal static class DefaultMetadataFactory
{
    internal static Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> GetFactory<TModel, TMetadata>() 
    {
        return (isSatisfied, results) => isSatisfied switch
        {
            true  => results
                .Where(result => result.IsSatisfied)
                .SelectMany(result => result.GetMetadata()),
            false => results
                .Where(result => !result.IsSatisfied)
                .SelectMany(result => result.GetMetadata())
        };
    }
    
    internal static Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> GetFactory<TModel, TMetadata, TUnderlyingMetadata>() => 
        (_, _) => Enumerable.Empty<TMetadata>();
}