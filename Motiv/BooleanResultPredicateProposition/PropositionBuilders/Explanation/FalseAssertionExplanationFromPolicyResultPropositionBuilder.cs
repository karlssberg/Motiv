namespace Motiv.BooleanResultPredicateProposition.PropositionBuilders.Explanation;

/// <summary>
/// A builder for creating propositions based on an existing proposition and explanation factories.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
public readonly ref struct FalseAssertionExplanationFromPolicyResultPropositionBuilder<TModel, TUnderlyingMetadata>(
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>> predicate,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, string> trueBecause)
{
    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A human-readable reason why the condition is false.</param>
    /// <returns>A factory for creating propositions based on the supplied proposition and explanation factories.</returns>
    public ExplanationFromPolicyResultPropositionFactory<TModel, TUnderlyingMetadata> WhenFalse(
        string falseBecause) =>
        new(predicate,
            trueBecause,
            (_, _) => falseBecause);

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>A factory for creating propositions based on the supplied proposition and explanation factories.</returns>
    public ExplanationFromPolicyResultPropositionFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<TModel, string> falseBecause) =>
        new(predicate,
            trueBecause,
            (model, _) => falseBecause(model));

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>A factory for creating propositions based on the supplied proposition and explanation factories.</returns>
    public ExplanationFromPolicyResultPropositionFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<TModel, PolicyResultBase<TUnderlyingMetadata>, string> falseBecause) =>
        new(predicate,
            trueBecause,
            falseBecause);

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a collection of human-readable reasons when the condition is false.</param>
    /// <returns>A factory for creating propositions based on the supplied proposition and explanation factories.</returns>
    public MultiAssertionExplanationFromPolicyPropositionFactory<TModel, TUnderlyingMetadata> WhenFalseYield(
        Func<TModel, PolicyResultBase<TUnderlyingMetadata>, IEnumerable<string>> falseBecause) =>
        new(predicate,
            trueBecause.ToEnumerableReturn(),
            falseBecause);
}
