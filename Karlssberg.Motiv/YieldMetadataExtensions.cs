using Karlssberg.Motiv.ChangeMetadataType;

namespace Karlssberg.Motiv;

public static class YieldMetadataExtensions
{
    public static SpecBase<IEnumerable<TModel>, TMetadata> Yield<TModel, TMetadata, TUnderlyingMetadata>(
        this SpecBase<IEnumerable<TModel>, TUnderlyingMetadata> spec,
        Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata) =>
        new ChangeHigherOrderMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(spec, metadata);

    public static SpecBase<IEnumerable<TModel>, TMetadata> Yield<TModel, TMetadata, TUnderlyingMetadata>(
        this SpecBase<IEnumerable<TModel>, TUnderlyingMetadata> spec,
        Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, TMetadata> metadata) =>
        new ChangeHigherOrderMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
            spec, 
            (isSatisfied, results) => [metadata(isSatisfied, results)]);
}