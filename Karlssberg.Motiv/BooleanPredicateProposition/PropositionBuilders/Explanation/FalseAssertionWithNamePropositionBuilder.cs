namespace Karlssberg.Motiv.BooleanPredicateProposition.PropositionBuilders.Explanation;

/// <summary>
/// A builder for creating propositions based on a predicate and a true condition, or for further refining a proposition.
/// </summary>
/// <typeparam name="TModel">The type of the model the proposition is for.</typeparam>
public readonly ref struct FalseAssertionWithNamePropositionBuilder<TModel>(
    Func<TModel, bool> predicate,
    string trueBecause)
{
    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A human-readable reason why the condition is false.</param>
    /// <returns>An instance of <see cref="ExplanationWithNamePropositionFactory{TModel}" />.</returns>
    public ExplanationWithNamePropositionFactory<TModel> WhenFalse(string falseBecause)
    {
        falseBecause.ThrowIfNullOrWhitespace(nameof(falseBecause));
        return new ExplanationWithNamePropositionFactory<TModel>(
            predicate,
            trueBecause,
            _ => falseBecause);
    }

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="ExplanationWithNamePropositionFactory{TModel}" />.</returns>
    public ExplanationWithNamePropositionFactory<TModel> WhenFalse(Func<TModel, string> falseBecause)
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        return new ExplanationWithNamePropositionFactory<TModel>(
            predicate,
            trueBecause,
            falseBecause);
    }
}