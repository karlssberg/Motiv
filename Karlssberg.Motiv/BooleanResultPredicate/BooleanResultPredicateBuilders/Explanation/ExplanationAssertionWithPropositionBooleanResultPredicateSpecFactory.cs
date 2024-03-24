namespace Karlssberg.Motiv.BooleanResultPredicate.BooleanResultPredicateBuilders.Explanation;

/// <summary>
/// A factory for creating specifications based on the supplied specification and explanation factories.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the specification.</typeparam>
public readonly ref struct ExplanationAssertionWithPropositionBooleanResultPredicateSpecFactory<TModel, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> predicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> falseBecause,
    string candidateProposition)
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

    /// <summary>
    /// Creates a specification with explanations for when the condition is true or false. The propositional statement
    /// will be obtained from the .WhenTrue() assertion.
    /// </summary>
    /// <returns>A specification for the model.</returns>
    public SpecBase<TModel, string> Create() =>
        new BooleanResultPredicateMultiMetadataSpec<TModel, string, TUnderlyingMetadata>(
            predicate,
            trueBecause,
            falseBecause,
            candidateProposition);
}