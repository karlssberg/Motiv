namespace Karlssberg.Motiv.Propositions.FirstOrderSpecBuilders.Reasons;

/// <summary>Represents an interface for building a specifcation.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata"></typeparam>
public readonly struct ReasonFirstOrderSpecFactory<TModel>(
    Func<TModel, bool> predicate,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause,
    string candidateDescription)
{
    /// <summary>Provide a human readable explanation for when the condition is false.</summary>
    /// <param name="description">The description of the specification. If not specified, the description of the specification</param>
    /// <returns>A specification base.</returns>
    public SpecBase<TModel, string> CreateSpec() =>
        new Spec<TModel, string>(
            candidateDescription,
            predicate,
            trueBecause,
            falseBecause);
    
    /// <summary>Provide a human readable explanation for when the condition is false.</summary>
    /// <param name="description">The description of the specification. If not specified, the description of the specification</param>
    /// <returns>A specification base.</returns>
    public SpecBase<TModel, string> CreateSpec(string description) =>
        new Spec<TModel, string>(
            description.ThrowIfNullOrWhitespace(nameof(description)),
            predicate,
            trueBecause,
            falseBecause);
}