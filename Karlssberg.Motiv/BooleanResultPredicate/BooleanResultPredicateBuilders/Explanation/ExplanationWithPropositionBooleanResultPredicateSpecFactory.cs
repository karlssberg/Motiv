namespace Karlssberg.Motiv.BooleanResultPredicate.BooleanResultPredicateBuilders.Explanation;

/// <summary>
/// A factory for creating specifications based on the supplied specification and explanation factories.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the specification.</typeparam>
public readonly ref struct ExplanationWithPropositionBooleanResultPredicateSpecFactory<TModel, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> predicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> falseBecause,
    string candidateProposition)
{
    /// <summary>
    /// Creates a specification with explanations for when the condition is true or false.
    /// </summary>
    /// <returns>A specification for the model.</returns>
    public SpecBase<TModel, string> Create() =>
        new BooleanResultPredicateExplanationSpec<TModel, TUnderlyingMetadata>(
            predicate,
            trueBecause,
            falseBecause,
            candidateProposition);

    /// <summary>
    /// Creates a specification with descriptive assertions, but using the supplied proposition to succinctly explain
    /// the decision.
    /// </summary>
    /// <param name="proposition">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public SpecBase<TModel, string> Create(string proposition) =>
        new BooleanResultPredicateExplanationSpec<TModel, TUnderlyingMetadata>(
            predicate,
            trueBecause,
            falseBecause,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}