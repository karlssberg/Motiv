namespace Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders.Metadata;

/// <summary>
/// A factory for creating specifications based on a predicate and metadata factories. This is particularly useful
/// for handling edge-case scenarios where it would be impossible or impractical to create a specification that covers
/// every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the specification.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the specification.</typeparam>
public readonly ref struct MetadataFromBooleanHigherOrderSpecFactory<TModel, TMetadata>(
    Func<TModel, bool> resultResolver,
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate, 
    Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenTrue, 
    Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenFalse,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>>? causeSelector)
{
    /// <summary>Creates a specification and names it with the propositional statement provided.</summary>
    /// <param name="proposition">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public SpecBase<IEnumerable<TModel>, TMetadata> Create(string proposition)
    {
        proposition.ThrowIfNullOrWhitespace(nameof(proposition));
        return new HigherOrderFromBooleanPredicateMetadataSpec<TModel,TMetadata>(
            resultResolver,
            higherOrderPredicate,
            whenTrue,
            whenFalse,
            new Proposition(proposition),
            causeSelector);
    }
}