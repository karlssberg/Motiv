using Karlssberg.Motiv.Proposition.FirstOrderSpecBuilders.Metadata;

namespace Karlssberg.Motiv.Proposition.FirstOrderSpecBuilders.Reasons;

/// <summary>Represents an interface for asking for a false reason in a specification.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public readonly struct YieldReasonWithDescriptionUnresolvedWhenFalse<TModel>(
    Func<TModel, bool> predicate,
    Func<TModel, string> trueBecause)
{
    /// <summary>Provide a human readable explanation for when the condition is false.</summary>
    /// <param name="falseBecause">A human readable explanation pf why the predicate returned false.</param>
    /// <returns>A specification base.</returns>
    public DescriptiveMetadataSpecFactory<TModel, string> WhenFalse(string falseBecause)
    {
        falseBecause.ThrowIfNullOrWhitespace(nameof(falseBecause));
        return new DescriptiveMetadataSpecFactory<TModel, string>(
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
    public DescriptiveMetadataSpecFactory<TModel, string> WhenFalse(Func<TModel, string> falseBecause)
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        return new DescriptiveMetadataSpecFactory<TModel, string>(
            predicate,
            trueBecause,
            falseBecause);
    }
}