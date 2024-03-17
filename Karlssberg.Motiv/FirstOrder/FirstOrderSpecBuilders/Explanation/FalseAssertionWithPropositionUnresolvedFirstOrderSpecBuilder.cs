using Karlssberg.Motiv.FirstOrder.FirstOrderSpecBuilders.Metadata;

namespace Karlssberg.Motiv.FirstOrder.FirstOrderSpecBuilders.Explanation;

/// <summary>Represents an interface for asking for a false reason in a specification.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public readonly ref struct FalseAssertionWithPropositionUnresolvedFirstOrderSpecBuilder<TModel>(
    Func<TModel, bool> predicate,
    Func<TModel, string> trueBecause)
{
    /// <summary>Provide a human readable explanation for when the condition is false.</summary>
    /// <param name="falseBecause">A human readable explanation pf why the predicate returned false.</param>
    /// <returns>A specification base.</returns>
    public ExplanationWithPropositionFirstOrderSpecFactory<TModel> WhenFalse(string falseBecause)
    {
        falseBecause.ThrowIfNullOrWhitespace(nameof(falseBecause));
        return new ExplanationWithPropositionFirstOrderSpecFactory<TModel>(
            predicate,
            trueBecause,
            _ => falseBecause);
    }

    /// <summary>Supply a function that when executed generates a human readable explanation for when the condition is false.</summary>
    /// <param name="falseBecause">
    ///     The function that evaluates the model and returns a human readable explanation of why the
    ///     predicate returned false.
    /// </param>
    /// <returns>A specification base.</returns>
    public ExplanationWithPropositionFirstOrderSpecFactory<TModel> WhenFalse(Func<TModel, string> falseBecause)
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        return new ExplanationWithPropositionFirstOrderSpecFactory<TModel>(
            predicate,
            trueBecause,
            falseBecause);
    }
}