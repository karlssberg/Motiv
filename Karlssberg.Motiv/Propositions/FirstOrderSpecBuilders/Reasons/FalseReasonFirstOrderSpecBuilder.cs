namespace Karlssberg.Motiv.Propositions.FirstOrderSpecBuilders.Reasons;

/// <summary>Represents an interface for specifying the behavior when a condition is false.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public readonly struct FalseReasonFirstOrderSpecBuilder<TModel>(
    Func<TModel, bool> predicate,
    Func<TModel, string> trueBecause,
    string description)
{
    /// <summary>Specifies the behavior when the condition is false.</summary>
    /// <param name="falseBecause">The metadata associated with the condition.</param>
    /// <returns>The specification with the specified metadata.</returns>
    public ReasonFirstOrderSpecFactory<TModel> WhenFalse(string falseBecause)
    {
        falseBecause.ThrowIfNullOrWhitespace(nameof(falseBecause));
        return new ReasonFirstOrderSpecFactory<TModel>(
            predicate,
            trueBecause, 
            _ => falseBecause, 
            description);
    }

    /// <summary>Supply a function that when executed generates a human readable explanation for when the condition is false.</summary>
    /// <param name="falseBecause">
    ///     The function that evaluates the model and returns a human readable explanation of why the
    ///     predicate returned false.
    /// </param>
    /// <returns>A specification base.</returns>
    public ReasonFirstOrderSpecFactory<TModel> WhenFalse(Func<TModel, string> falseBecause)
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        return new ReasonFirstOrderSpecFactory<TModel>(
            predicate, 
            trueBecause,
            falseBecause,
            description);
    }
}