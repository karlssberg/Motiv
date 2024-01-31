using Karlssberg.Motiv.ChangeMetadata;

namespace Karlssberg.Motiv.ChangeHigherOrderMetadata;

internal class ChangeHigherOrderMetadataSpec<TModel, TMetadata>(
    SpecBase<IEnumerable<TModel>, TMetadata> higherOrderSpec,
    Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> metadataFactory)
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
 
        return new ChangeHigherOrderMetadataBooleanResults<TMetadata>(
            metadata,
            booleanResult);
    }

    private static BooleanResultWithModel<TModel, TMetadata> CreateBooleanResultWithModel(
        BooleanResultBase<TMetadata> result, TModel model) => new(model, result);
}