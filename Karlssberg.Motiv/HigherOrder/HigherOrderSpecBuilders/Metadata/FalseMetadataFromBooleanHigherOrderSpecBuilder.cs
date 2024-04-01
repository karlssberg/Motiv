namespace Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders.Metadata;

/// <summary>
/// A builder for creating propositions based on a predicate and metadata factories. This is particularly useful
/// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
/// every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the specification.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the specification.</typeparam>
public readonly ref struct FalseMetadataFromBooleanHigherOrderSpecBuilder<TModel, TMetadata>(
    Func<TModel, bool> resultResolver,
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate, 
    Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenTrue, 
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>>? causeSelector)
{
    /// <summary>Specifies the metadata to use when the condition is false.</summary>
    /// <param name="whenFalse">The metadata to use when the condition is false.</param>
    /// <returns>An instance of <see cref="MetadataHigherOrderSpecFactory{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public MetadataFromBooleanHigherOrderSpecFactory<TModel, TMetadata> WhenFalse(TMetadata whenFalse) =>
        new(resultResolver,
            higherOrderPredicate,
            whenTrue,
            _ => whenFalse,
            causeSelector);

    /// <summary>Specifies a metadata factory function to use when the condition is false.</summary>
    /// <param name="whenFalse">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="MetadataHigherOrderSpecFactory{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public MetadataFromBooleanHigherOrderSpecFactory<TModel, TMetadata> WhenFalse(
        Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenFalse) =>
        new(resultResolver,
            higherOrderPredicate,
            whenTrue,
            whenFalse,
            causeSelector);
}