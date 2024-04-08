namespace Karlssberg.Motiv.HigherOrderProposition.PropositionBuilders.Metadata;

/// <summary>
/// A factory for creating propositions based on a predicate and metadata factories. This is particularly useful
/// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
/// every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the specification.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the specification.</typeparam>
public readonly ref struct MetadataHigherOrderPropositionFactory<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>,
        IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>>? causeSelector)
{
    /// <summary>Creates a specification and names it with the propositional statement provided.</summary>
    /// <param name="proposition">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public SpecBase<IEnumerable<TModel>, TMetadata> Create(string proposition)
    {
        proposition.ThrowIfNullOrWhitespace(nameof(proposition));
        return new HigherOrderMultiMetadataProposition<TModel, TMetadata, TUnderlyingMetadata>(
            spec.IsSatisfiedByWithExceptionRethrowing,
            higherOrderPredicate,
            whenTrue,
            whenFalse,
            new HigherOrderProposition<TModel, TUnderlyingMetadata>(proposition, spec),
            causeSelector);
    }
}