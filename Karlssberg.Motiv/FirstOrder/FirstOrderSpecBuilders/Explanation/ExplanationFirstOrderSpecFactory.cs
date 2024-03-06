namespace Karlssberg.Motiv.FirstOrder.FirstOrderSpecBuilders.Explanation;

/// <summary>Represents an interface for building a specifcation.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata"></typeparam>
public readonly ref struct ExplanationFirstOrderSpecFactory<TModel>(
    Func<TModel, bool> predicate,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause,
    string candidateProposition)
{
    /// <summary>Provide a human readable explanation for when the condition is false.</summary>
    /// <returns>A specification base.</returns>
    public SpecBase<TModel, string> CreateSpec() =>
        new ExplanationSpec<TModel>(
            predicate,
            trueBecause,
            falseBecause,
            candidateProposition);
    
    /// <summary>Provide a human readable explanation for when the condition is false.</summary>
    /// <param name="proposition">The description of the specification. If not specified, the description of the specification</param>
    /// <returns>A specification base.</returns>
    public SpecBase<TModel, string> CreateSpec(string proposition) =>
        new ExplanationSpec<TModel>(
            predicate,
            trueBecause,
            falseBecause, 
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}