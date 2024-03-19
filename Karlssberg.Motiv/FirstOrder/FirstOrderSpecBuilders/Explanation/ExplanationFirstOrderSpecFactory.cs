namespace Karlssberg.Motiv.FirstOrder.FirstOrderSpecBuilders.Explanation;

/// <summary>A factory for creating specifications based on a predicate and explanations for true and false conditions.</summary>
/// <typeparam name="TModel">The type of the model the specification is for.</typeparam>
public readonly ref struct ExplanationFirstOrderSpecFactory<TModel>(
    Func<TModel, bool> predicate,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause,
    string candidateProposition)
{
    /// <summary>Creates a specification with explanations for when the condition is true or false.</summary>
    /// <returns>An instance of <see cref="SpecBase{TModel, string}" />.</returns>
    public SpecBase<TModel, string> Create() =>
        new ExplanationSpec<TModel>(
            predicate,
            trueBecause,
            falseBecause,
            candidateProposition);

    /// <summary>
    /// Creates a specification with descriptive assertions, but using the supplied proposition to succinctly explain
    /// the decision.
    /// </summary>
    /// <param name="proposition">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>An instance of <see cref="SpecBase{TModel, string}" />.</returns>
    public SpecBase<TModel, string> Create(string proposition) =>
        new MetadataSpec<TModel, string>(
            predicate,
            trueBecause,
            falseBecause,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}