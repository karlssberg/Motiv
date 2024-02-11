using Karlssberg.Motiv.ChangeMetadataType.YieldWhenFalse;

namespace Karlssberg.Motiv.ChangeMetadataType;

internal class ChangeHigherOrderMetadataTypeBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<IEnumerable<TModel>, TUnderlyingMetadata> spec, 
    Func<IEnumerable<TModel>, IEnumerable<TModel>, TMetadata> whenTrue)
    : IYieldHigherOrderMetadataWhenFalse<TModel, TMetadata, TUnderlyingMetadata>
{
    public SpecBase<IEnumerable<TModel>, TMetadata> WhenFalse(
        Func<IEnumerable<TModel>, IEnumerable<TModel>, TMetadata> whenFalse)
    {
        whenFalse.ThrowIfNull(nameof(whenFalse));
        return new ChangeHigherOrderMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
            spec,
            (isSatisfied, underlyingResults) =>
            {
                var underlyingResultsArray = underlyingResults.ToArray();
                var satisfied = underlyingResultsArray
                    .GetModelsWhere(result => result.Satisfied == isSatisfied);
                
                var unsatisfied = underlyingResultsArray
                    .GetModelsWhere(result => result.Satisfied != isSatisfied);

                var metadata = isSatisfied switch
                {
                    true => whenTrue(satisfied, unsatisfied),
                    false => whenFalse(satisfied, unsatisfied),
                };

                return [metadata];
            });
    }
}