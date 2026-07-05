using Motiv.BooleanPredicateProposition.PropositionBuilders.Overloads;
using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.BooleanPredicateProposition.PropositionBuilders;

/// <summary>A factory for creating propositions based on a predicate and explanations for true and false conditions.</summary>
/// <param name="predicate">The predicate function that evaluates the model to a boolean value.</param>
/// <param name="trueBecause">The explanation for when the predicate is true.</param>
/// <param name="falseBecause">The explanation for when the predicate is false.</param>
/// <typeparam name="TModel">The type of the model the proposition is for.</typeparam>
[FluentTarget(typeof(Spec), TerminalMethod = TerminalMethod.None)]
public readonly struct ExplanationWithNamePropositionFactory<TModel>(
    [FluentMethod("Build")]Func<TModel, bool> predicate,
    [FluentMethod("WhenTrue")]string trueBecause,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<TModel, string> falseBecause)
{
    /// <summary>
    /// Creates a proposition with explanations for when the condition is true or false. The propositional statement
    /// will be obtained from the .WhenTrue() assertion.
    /// </summary>
    /// <returns>An instance of <see cref="PolicyBase{TModel, TMetadata}" />.</returns>
    public PolicyBase<TModel, string> Create()
    {
        predicate.ThrowIfNull(nameof(predicate));
        trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));
        return new ExplanationProposition<TModel>(
            predicate,
            trueBecause.ToFunc<TModel, string>(),
            falseBecause,
            new SpecDescription(trueBecause));
    }

    /// <summary>
    /// Creates a proposition with descriptive assertions, but using the supplied proposition to succinctly explain
    /// the decision.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>An instance of <see cref="PolicyBase{TModel, TMetadata}" />.</returns>
    public PolicyBase<TModel, string> Create(string statement)
    {
        predicate.ThrowIfNull(nameof(predicate));
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new Proposition<TModel, string>(
            predicate,
            trueBecause.ToFunc<TModel, string>(),
            falseBecause,
            new SpecDescription(statement));
    }
}
