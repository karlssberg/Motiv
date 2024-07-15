namespace Motiv.HigherOrderProposition.PropositionBuilders.Metadata;

/// <summary>
/// A builder for creating propositions based on a predicate and metadata factories. This is particularly useful
/// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
/// every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the specification.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the specification.</typeparam>
public readonly ref struct FalseMultiMetadataFromPolicyResultHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>> resultResolver,
    Func<IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<bool,
        IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>,
        IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>> causeSelector)
{
    /// <summary>Specifies the metadata to use when the condition is false.</summary>
    /// <param name="whenFalse">The metadata to use when the condition is false.</param>
    /// <returns>An instance of <see cref="MetadataHigherOrderPropositionFactory{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public MultiMetadataFromPolicyResultHigherOrderPropositionFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(TMetadata whenFalse) =>
        new(resultResolver,
            higherOrderPredicate,
            whenTrue,
            _ => whenFalse.ToEnumerable(),
            causeSelector);

    /// <summary>Specifies a metadata factory function to use when the condition is false.</summary>
    /// <param name="whenFalse">A function that generates metadata when the condition is false.</param>
    /// <returns>An instance of <see cref="MetadataHigherOrderPropositionFactory{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public MultiMetadataFromPolicyResultHigherOrderPropositionFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, TMetadata> whenFalse) =>
        new(resultResolver,
            higherOrderPredicate,
            whenTrue,
            results => whenFalse(results).ToEnumerable(),
            causeSelector);

    /// <summary>Specifies a metadata factory function to use when the condition is false.</summary>
    /// <param name="whenFalse">A function that generates a collection of metadata when the condition is false.</param>
    /// <returns>An instance of <see cref="MetadataHigherOrderPropositionFactory{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public MultiMetadataFromPolicyResultHigherOrderPropositionFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalseYield(
        Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse) =>
        new(resultResolver,
            higherOrderPredicate,
            whenTrue,
            whenFalse,
            causeSelector);
}
