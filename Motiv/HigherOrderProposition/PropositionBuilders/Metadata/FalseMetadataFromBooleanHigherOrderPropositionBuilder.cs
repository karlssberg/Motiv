namespace Motiv.HigherOrderProposition.PropositionBuilders.Metadata;

/// <summary>
/// A builder for creating propositions based on a predicate and metadata factories. This is particularly useful
/// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
/// every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the specification.</typeparam>
public readonly ref struct FalseMetadataFromBooleanHigherOrderPropositionBuilder<TModel, TMetadata>(
    Func<TModel, bool> resultResolver,
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenTrue,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector)
{
    /// <summary>Specifies the metadata to use when the condition is false.</summary>
    /// <param name="whenFalse">The metadata to use when the condition is false.</param>
    /// <returns>An instance of <see cref="MetadataHigherOrderPropositionFactory{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public MetadataFromBooleanHigherOrderPropositionFactory<TModel, TMetadata> WhenFalse(TMetadata whenFalse) =>
        new(resultResolver,
            higherOrderPredicate,
            whenTrue,
            _ => whenFalse,
            causeSelector);

    /// <summary>Specifies a metadata factory function to use when the condition is false.</summary>
    /// <param name="whenFalse">A function that generates metadata when the condition is false.</param>
    /// <returns>An instance of <see cref="MetadataHigherOrderPropositionFactory{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public MetadataFromBooleanHigherOrderPropositionFactory<TModel, TMetadata> WhenFalse(
        Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenFalse) =>
        new(resultResolver,
            higherOrderPredicate,
            whenTrue,
            whenFalse,
            causeSelector);

    /// <summary>Specifies a metadata factory function to use when the condition is false.</summary>
    /// <param name="whenFalse">A function that generates a collecton of metadata when the condition is false.</param>
    /// <returns>An instance of <see cref="MetadataHigherOrderPropositionFactory{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public MultiMetadataFromBooleanHigherOrderPropositionFactory<TModel, TMetadata> WhenFalseYield(
        Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<TMetadata>> whenFalse) =>
        new(resultResolver,
            higherOrderPredicate,
            whenTrue.ToEnumerableReturn(),
            whenFalse,
            causeSelector);
}
