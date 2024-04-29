namespace Motiv.HigherOrderProposition.PropositionBuilders.Metadata;


/// <summary>
/// A builder for creating propositions based on a predicate and metadata factories. This is particularly useful
/// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
/// every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the specification.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the specification.</typeparam>
public readonly ref struct FalseMetadataFromBooleanResultHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> resultResolver,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>,
        IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector)
{
    /// <summary>Specifies the metadata to use when the condition is false.</summary>
    /// <param name="whenFalse">The metadata to use when the condition is false.</param>
    /// <returns>An instance of <see cref="MetadataHigherOrderPropositionFactory{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public MetadataFromBooleanResultHigherOrderPropositionFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(TMetadata whenFalse) =>
        new(resultResolver,
            higherOrderPredicate,
            whenTrue,
            _ => whenFalse.ToEnumerable(),
            causeSelector);

    /// <summary>Specifies a metadata factory function to use when the condition is false.</summary>
    /// <param name="whenFalse">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="MetadataHigherOrderPropositionFactory{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public MetadataFromBooleanResultHigherOrderPropositionFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, TMetadata> whenFalse) =>
        new(resultResolver,
            higherOrderPredicate,
            whenTrue,
            results => whenFalse(results).ToEnumerable(),
            causeSelector);

    /// <summary>Specifies a metadata factory function to use when the condition is false.</summary>
    /// <param name="whenFalse">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="MetadataHigherOrderPropositionFactory{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public MetadataFromBooleanResultHigherOrderPropositionFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse) =>
        new(resultResolver,
            higherOrderPredicate,
            whenTrue,
            whenFalse,
            causeSelector);
}