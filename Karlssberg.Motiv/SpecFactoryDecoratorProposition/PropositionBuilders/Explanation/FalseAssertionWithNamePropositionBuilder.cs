namespace Karlssberg.Motiv.SpecFactoryDecoratorProposition.PropositionBuilders.Explanation;

/// <summary>
/// Represents a builder for creating propositions based on a predicate and explanations for true and false conditions. This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
public readonly ref struct FalseAssertionWithNamePropositionBuilder<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> trueBecause,
    string candidateProposition)
{
    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A human-readable reason why the condition is false.</param>
    /// <returns>An instance of <see cref="ExplanationWithNamePropositionFactory{TModel,TUnderlyingMetadata}" />.</returns>
    public ExplanationWithNamePropositionFactory<TModel, TUnderlyingMetadata> WhenFalse(string falseBecause) =>
        new(specPredicate,
            trueBecause,
            (_, _) => falseBecause,
            candidateProposition);

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="ExplanationWithNamePropositionFactory{TModel,TUnderlyingMetadata}" />.</returns>
    public ExplanationWithNamePropositionFactory<TModel, TUnderlyingMetadata> WhenFalse(Func<TModel, string> falseBecause) =>
        new(specPredicate,
            trueBecause,
            (model, _) => falseBecause(model),
            candidateProposition);

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="ExplanationWithNamePropositionFactory{TModel,TUnderlyingMetadata}" />.</returns>
    public ExplanationWithNamePropositionFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> falseBecause) =>
        new(specPredicate,
            trueBecause,
            falseBecause,
            candidateProposition);

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="MultiAssertionWithNameExplanationPropositionFactory{TModel,TUnderlyingMetadata}" />.</returns>
    public MultiAssertionWithNameExplanationPropositionFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> falseBecause) =>
        new(specPredicate,
            trueBecause.ToEnumerableReturn(),
            falseBecause,
            candidateProposition);
}