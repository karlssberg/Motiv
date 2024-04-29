namespace Motiv.BooleanPredicateProposition.PropositionBuilders.Explanation;

/// <summary>A factory for creating propositions based on a predicate and explanations for true and false conditions.</summary>
/// <typeparam name="TModel">The type of the model the proposition is for.</typeparam>
public readonly ref struct ExplanationWithNamePropositionFactory<TModel>(
    Func<TModel, bool> predicate,
    string trueBecause,
    Func<TModel, string> falseBecause)
{
    /// <summary>
    /// Creates a proposition with explanations for when the condition is true or false. The propositional statement
    /// will be obtained from the .WhenTrue() assertion.
    /// </summary>
    /// <returns>An instance of <see cref="SpecBase{TModel, TMetadata}" />.</returns>
    public SpecBase<TModel, string> Create() =>
        new ExplanationProposition<TModel>(
            predicate,
            trueBecause.ToFunc<TModel, string>(),
            falseBecause,
            new SpecDescription(trueBecause));

    /// <summary>
    /// Creates a proposition with descriptive assertions, but using the supplied proposition to succinctly explain
    /// the decision.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>An instance of <see cref="SpecBase{TModel, TMetadata}" />.</returns>
    public SpecBase<TModel, string> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new ExplanationProposition<TModel>(
            predicate,
            trueBecause.ToFunc<TModel, string>(),
            falseBecause,
            new SpecDescription(statement));
    }
}