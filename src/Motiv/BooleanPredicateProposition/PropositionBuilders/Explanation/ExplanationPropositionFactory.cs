using Motiv.Generator.Attributes;
using Motiv.Shared;

namespace Motiv.BooleanPredicateProposition.PropositionBuilders.Explanation;

/// <summary>
/// A factory for creating propositions based on a predicate and explanations for true and false conditions.
/// </summary>
/// <typeparam name="TModel">The type of the model the proposition is for.</typeparam>
[FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
public readonly partial struct ExplanationPropositionFactory<TModel>(
    [FluentMethod("Build")]Func<TModel, bool> predicate,
    [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<TModel, string> whenTrue,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<TModel, string> whenFalse)
{
    /// <summary>
    /// Creates a proposition with descriptive assertions, but using the supplied proposition to succinctly explain
    /// the decision.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>An instance of <see cref="SpecBase{TModel, TMetadata}" />.</returns>
    public PolicyBase<TModel, string> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new ExplanationProposition<TModel>(
            predicate,
            whenTrue,
            whenFalse,
            new SpecDescription(statement));
    }
}
