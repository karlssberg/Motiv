using Karlssberg.Motiv.ChangeHigherOrderMetadata;

namespace Karlssberg.Motiv;

public static class YieldMetadataExtensions
{
    public static SpecBase<IEnumerable<TModel>, TMetadata> Yield<TModel, TMetadata>(
        this SpecBase<IEnumerable<TModel>, TMetadata> spec,
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> metadata) =>
        new ChangeHigherOrderMetadataSpec<TModel, TMetadata>(spec, metadata);

    public static SpecBase<IEnumerable<TModel>, TMetadata> Yield<TModel, TMetadata>(
        this SpecBase<IEnumerable<TModel>, TMetadata> spec,
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, TMetadata> metadata) =>
        new ChangeHigherOrderMetadataSpec<TModel, TMetadata>(
            spec, 
            (isSatisfied, results) => [metadata(isSatisfied, results)]);
}