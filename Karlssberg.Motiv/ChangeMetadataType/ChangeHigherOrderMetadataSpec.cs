namespace Karlssberg.Motiv.ChangeMetadataType;

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
        
        var metadata = metadataFactory(booleanResult.Satisfied,
            underlyingResults);
 
        return new ChangeHigherOrderMetadataBooleanResult<TMetadata, TUnderlyingMetadata>(
            metadata,
            booleanResult);
    }

    private static BooleanResultWithModel<TModel, TUnderlyingMetadata> CreateBooleanResultWithModel(
        BooleanResultBase<TUnderlyingMetadata> result, TModel model) => new(model, result);
}