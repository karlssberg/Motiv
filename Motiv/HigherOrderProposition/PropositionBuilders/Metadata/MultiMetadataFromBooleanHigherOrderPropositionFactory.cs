namespace Motiv.HigherOrderProposition.PropositionBuilders.Metadata;

/// <summary>
/// A factory for creating propositions based on a predicate and metadata factories. This is particularly useful
/// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
/// every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the specification.</typeparam>
public readonly ref struct MultiMetadataFromBooleanHigherOrderPropositionFactory<TModel, TMetadata>(
    Func<TModel, bool> resultResolver,
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate, 
    Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<TMetadata>> whenTrue, 
    Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<TMetadata>> whenFalse,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector)
{
    /// <summary>Creates a specification and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public SpecBase<IEnumerable<TModel>, TMetadata> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new HigherOrderFromBooleanPredicateMultiMetadataProposition<TModel,TMetadata>(
            resultResolver,
            higherOrderPredicate,
            whenTrue,
            whenFalse,
            new SpecDescription(statement),
            causeSelector);
    }
}
