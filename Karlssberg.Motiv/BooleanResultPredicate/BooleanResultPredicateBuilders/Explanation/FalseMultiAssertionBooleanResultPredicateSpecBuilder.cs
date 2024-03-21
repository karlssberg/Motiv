namespace Karlssberg.Motiv.BooleanResultPredicate.BooleanResultPredicateBuilders.Explanation;

/// <summary>
/// A builder for creating specifications based on an existing specification and explanation factories.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a specification that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the specification.</typeparam>
public readonly ref struct FalseMultiAssertionBooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> predicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> trueBecause)
{
    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A human-readable reason why the condition is false.</param>
    /// <returns>A factory for creating specifications based on the supplied specification and explanation factories.</returns>
    public ExplanationBooleanResultPredicateSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        string falseBecause) =>
        new(predicate,
            trueBecause,
            (_, _) => falseBecause.ToEnumerable());

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>A factory for creating specifications based on the supplied specification and explanation factories.</returns>
    public ExplanationBooleanResultPredicateSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<TModel, string> falseBecause) =>
        new(predicate,
            trueBecause,
            (model, _) => falseBecause(model).ToEnumerable());

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>A factory for creating specifications based on the supplied specification and explanation factories.</returns>
    public ExplanationBooleanResultPredicateSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> falseBecause) =>
        new(predicate,
            trueBecause,
            falseBecause.ToEnumerableReturn());

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a collection of human-readable reasons when the condition is false.</param>
    /// <returns>A factory for creating specifications based on the supplied specification and explanation factories.</returns>
    public ExplanationMultiAssertionBooleanResultPredicateSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> falseBecause) =>
        new(predicate,
            trueBecause,
            falseBecause);
}