namespace Karlssberg.Motiv.FirstOrder.FirstOrderSpecBuilders.Explanation;

/// <summary>
/// A factory for creating specifications based on a predicate and explanations for true and false conditions.
/// </summary>
/// <typeparam name="TModel">The type of the model the specification is for.</typeparam>
public readonly ref struct ExplanationWithPropositionFirstOrderSpecFactory<TModel>(
    Func<TModel, bool> predicate,
    Func<TModel, string> whenTrue,
    Func<TModel, string> whenFalse)
{
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
            whenTrue,
            whenFalse,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}