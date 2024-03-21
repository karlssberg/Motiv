namespace Karlssberg.Motiv.BooleanResultPredicate.BooleanResultPredicateBuilders.Explanation;

/// <summary>
/// A factory for creating specifications based on the supplied specification and explanation factories.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a specification that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the specification.</typeparam>
public readonly ref struct ExplanationMultiAssertionBooleanResultPredicateSpecFactory<TModel, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> predicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> falseBecause)
{
    /// <summary>
    /// Creates a specification and names it with the propositional statement provided.
    /// </summary>
    /// <param name="proposition">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public SpecBase<TModel, string> Create(string proposition) =>
        new BooleanResultPredicateMultiMetadataSpec<TModel, string, TUnderlyingMetadata>(
            predicate,
            trueBecause,
            falseBecause,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}