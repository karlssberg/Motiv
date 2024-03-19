namespace Karlssberg.Motiv.FirstOrder.FirstOrderSpecBuilders.Explanation;

/// <summary>
/// A builder for creating specifications based on a predicate and a true condition, or for further refining a specification.
/// </summary>
/// <typeparam name="TModel">The type of the model the specification is for.</typeparam>
public readonly ref struct FalseAssertionWithPropositionUnresolvedFirstOrderSpecBuilder<TModel>(
    Func<TModel, bool> predicate,
    Func<TModel, string> trueBecause)
{
    /// <summary>
    /// Specifies a reason why the condition is false.
    /// </summary>
    /// <param name="falseBecause">A human-readable reason why the condition is false.</param>
    /// <returns>An instance of <see cref="ExplanationWithPropositionFirstOrderSpecFactory{TModel}" />.</returns>
    public ExplanationWithPropositionFirstOrderSpecFactory<TModel> WhenFalse(string falseBecause)
    {
        falseBecause.ThrowIfNullOrWhitespace(nameof(falseBecause));
        return new ExplanationWithPropositionFirstOrderSpecFactory<TModel>(
            predicate,
            trueBecause,
            _ => falseBecause);
    }

    /// <summary>
    /// Specifies a reason why the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="ExplanationWithPropositionFirstOrderSpecFactory{TModel}" />.</returns>
    public ExplanationWithPropositionFirstOrderSpecFactory<TModel> WhenFalse(Func<TModel, string> falseBecause)
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        return new ExplanationWithPropositionFirstOrderSpecFactory<TModel>(
            predicate,
            trueBecause,
            falseBecause);
    }
}