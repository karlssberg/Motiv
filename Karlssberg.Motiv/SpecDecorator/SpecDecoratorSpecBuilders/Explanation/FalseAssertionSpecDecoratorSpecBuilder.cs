namespace Karlssberg.Motiv.SpecDecorator.SpecDecoratorSpecBuilders.Explanation;

/// <summary>
/// A builder for creating propositions based on an existing proposition and explanation factories.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
public readonly ref struct FalseAssertionSpecDecoratorSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> trueBecause)
{
    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A human-readable reason why the condition is false.</param>
    /// <returns>A factory for creating propositions based on the supplied proposition and explanation factories.</returns>
    public ExplanationSpecDecoratorSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        string falseBecause)
    {
        falseBecause.ThrowIfNullOrWhitespace(nameof(falseBecause));
        return new ExplanationSpecDecoratorSpecFactory<TModel, TUnderlyingMetadata>(
            spec,
            trueBecause,
            (_, _) => falseBecause);
    }

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>A factory for creating propositions based on the supplied proposition and explanation factories.</returns>
    public ExplanationSpecDecoratorSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<TModel, string> falseBecause)
    {
        return new ExplanationSpecDecoratorSpecFactory<TModel, TUnderlyingMetadata>(
            spec,
            trueBecause,
            (model, _) => falseBecause(model));
    }

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>A factory for creating propositions based on the supplied specification and explanation factories.</returns>
    public ExplanationSpecDecoratorSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> falseBecause)
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        return new ExplanationSpecDecoratorSpecFactory<TModel, TUnderlyingMetadata>(
            spec,
            trueBecause,
            falseBecause);
    }

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a collection of human-readable reasons when the condition is false.</param>
    /// <returns>A factory for creating specifications based on the supplied specification and explanation factories.</returns>
    public ExplanationMultiAssertionSpecDecoratorSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> falseBecause)
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        return new ExplanationMultiAssertionSpecDecoratorSpecFactory<TModel, TUnderlyingMetadata>(
            spec,
            trueBecause.ToEnumerableReturn(),
            falseBecause);
    }
}