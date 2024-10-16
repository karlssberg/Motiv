namespace Motiv.HigherOrderProposition.PropositionBuilders.Metadata.Spec;

/// <summary>
/// A builder for creating propositions based on a predicate and metadata factories. This is particularly useful
/// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
/// every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the proposition.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the specification.</typeparam>
public readonly ref struct FalseMetadataHigherOrderPropositionBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>, TMetadata> whenTrue,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>,
        IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector)
{
    /// <summary>Specifies the metadata to use when the condition is false.</summary>
    /// <param name="whenFalse">The metadata to use when the condition is false.</param>
    /// <returns>An instance of <see cref="MetadataHigherOrderPropositionFactory{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public MetadataHigherOrderPropositionFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(TMetadata whenFalse) =>
        new(spec,
            higherOrderPredicate,
            whenTrue,
            _ => whenFalse,
            causeSelector);

    /// <summary>Specifies a metadata factory function to use when the condition is false.</summary>
    /// <param name="whenFalse">A function that generates metadata when the condition is false.</param>
    /// <returns>An instance of <see cref="MetadataHigherOrderPropositionFactory{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public MetadataHigherOrderPropositionFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        Func<HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>, TMetadata> whenFalse) =>
        new(spec,
            higherOrderPredicate,
            whenTrue,
            whenFalse,
            causeSelector);

    /// <summary>Specifies a metadata factory function to use when the condition is false.</summary>
    /// <param name="whenFalse">A function that generates a collection of metadata when the condition is false.</param>
    /// <returns>An instance of <see cref="MetadataHigherOrderPropositionFactory{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public MultiMetadataFromSpecHigherOrderPropositionFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalseYield(
        Func<HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse) =>
        new(spec,
            higherOrderPredicate,
            whenTrue.ToEnumerableReturn(),
            whenFalse,
            causeSelector);
}
