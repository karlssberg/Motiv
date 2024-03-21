namespace Karlssberg.Motiv.Composite.CompositeSpecBuilders.Explanation;

/// <summary>
/// A builder for creating specifications based on an existing specification and explanation factories.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a specification that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the specification.</typeparam>
public readonly ref struct FalseAssertionCompositeSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> trueBecause)
{
    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A human-readable reason why the condition is false.</param>
    /// <returns>A factory for creating specifications based on the supplied specification and explanation factories.</returns>
    public ExplanationCompositeSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        string falseBecause) =>
        new(spec,
            trueBecause,
            (_, _) => falseBecause);

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>A factory for creating specifications based on the supplied specification and explanation factories.</returns>
    public ExplanationCompositeSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<TModel, string> falseBecause) =>
        new(spec,
            trueBecause,
            (model, _) => falseBecause(model));

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>A factory for creating specifications based on the supplied specification and explanation factories.</returns>
    public ExplanationCompositeSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> falseBecause) =>
        new(spec,
            trueBecause,
            falseBecause);

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a collection of human-readable reasons when the condition is false.</param>
    /// <returns>A factory for creating specifications based on the supplied specification and explanation factories.</returns>
    public ExplanationMultiAssertionCompositeSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> falseBecause) =>
        new(spec,
            trueBecause.ToEnumerableReturn(),
            falseBecause);
}