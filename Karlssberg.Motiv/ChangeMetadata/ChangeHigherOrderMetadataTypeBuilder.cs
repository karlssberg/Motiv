using Karlssberg.Motiv.ChangeMetadata.YieldWhenFalse;

namespace Karlssberg.Motiv.ChangeMetadata;

internal class ChangeHigherOrderMetadataTypeBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<IEnumerable<TModel>, TUnderlyingMetadata> spec, 
    Func<IEnumerable<TModel>, IEnumerable<TModel>, TMetadata> whenTrue) :
    IYieldHigherOrderMetadataWhenFalse<TModel, TMetadata, TUnderlyingMetadata>
{
    public SpecBase<IEnumerable<TModel>, TMetadata> YieldWhenFalse(
        Func<IEnumerable<TModel>, IEnumerable<TModel>, TMetadata> whenFalse)
    {
        whenFalse.ThrowIfNull(nameof(whenFalse));
        return new ChangeHigherOrderMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
            spec,
            (isSatisfied, underlyingResults) =>
            {
                var underlyingResultsArray = underlyingResults.ToArray();
                var satisfied = underlyingResultsArray
                    .GetModelsWhere(result => result.Value == isSatisfied);
                
                var unsatisfied = underlyingResultsArray
                    .GetModelsWhere(result => result.Value != isSatisfied);

                var metadata = isSatisfied switch
                {
                    true => whenTrue(satisfied, unsatisfied),
                    false => whenFalse(satisfied, unsatisfied),
                };

                return [metadata];
            });
    }
}

internal class ChangeHigherOrderMetadataTypeWithMixedYieldBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<IEnumerable<TModel>, TUnderlyingMetadata> spec, 
    Func< IEnumerable<TModel>, TMetadata> whenTrue) :
    IYieldHigherOrderMetadataWhenFalse<TModel, TMetadata, TUnderlyingMetadata>
{
    public SpecBase<IEnumerable<TModel>, TMetadata> YieldWhenFalse(
        Func<IEnumerable<TModel>, IEnumerable<TModel>, TMetadata> whenFalse)
    {
        whenFalse.ThrowIfNull(nameof(whenFalse));
        return new ChangeHigherOrderMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
            spec,
            (isSatisfied, underlyingResults) =>
            {
                var underlyingResultsArray = underlyingResults.ToArray();
                var satisfied = underlyingResultsArray
                    .GetModelsWhere(result => result.Value == isSatisfied);
                
                var unsatisfied = underlyingResultsArray
                    .GetModelsWhere(result => result.Value != isSatisfied);

                var metadata = isSatisfied switch
                {
                    true => whenTrue(underlyingResultsArray.Select(result => result.Model)),
                    false => whenFalse(satisfied, unsatisfied)
                };

                return [metadata];
            });
    }
}