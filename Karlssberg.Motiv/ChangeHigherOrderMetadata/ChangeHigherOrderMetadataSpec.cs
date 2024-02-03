using System.Diagnostics;

namespace Karlssberg.Motiv.ChangeHigherOrderMetadata;

internal class ChangeHigherOrderMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<IEnumerable<TModel>, TUnderlyingMetadata> higherOrderSpec,
    Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadataFactory)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    /// <summary>Gets the description of the specification.</summary>
    public override string Description => higherOrderSpec.Description;

    /// <summary>Determines if the specification is satisfied by the given model.</summary>
    /// <param name="model">The model to be evaluated.</param>
    /// <returns>
    ///     A <see cref="BooleanResultBase{TMetadata}" /> indicating if the specification is satisfied and the resulting
    ///     metadata.
    /// </returns>
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var modelsArray = models.ToArray();
        var booleanResult = higherOrderSpec.IsSatisfiedBy(modelsArray);
        var underlyingResults = booleanResult.UnderlyingResults
            .Zip(modelsArray, CreateBooleanResultWithModel);
        
        var metadata = metadataFactory(booleanResult.IsSatisfied,
            underlyingResults);
 
        return new ChangeHigherOrderMetadataBooleanResult<TMetadata, TUnderlyingMetadata>(
            metadata,
            booleanResult);
    }

    private static BooleanResultWithModel<TModel, TUnderlyingMetadata> CreateBooleanResultWithModel(
        BooleanResultBase<TUnderlyingMetadata> result, TModel model) => new(model, result);
}



public interface IYieldHigherOrderMetadataWhenFalse<TModel, TMetadata, TUnderlyingMetadata>
{
    SpecBase<IEnumerable<TModel>, TMetadata> YieldWhenFalse(Func<IEnumerable<TModel>, IEnumerable<TModel>, TMetadata> whenFalse);
}

internal class ChangeHigherOrderHigherOrderMetadataTypeBuilder<TModel, TMetadata, TUnderlyingMetadata>(
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
                var trueResults = underlyingResultsArray
                    .GetModelsWhere(result => result.IsSatisfied == isSatisfied);
                
                var falseResults = underlyingResultsArray
                    .GetModelsWhere(result => result.IsSatisfied != isSatisfied);
                
                var metadata = isSatisfied
                    ? whenTrue(trueResults, falseResults)
                    : whenFalse(trueResults, falseResults);

                return [metadata];
            });
    }

}