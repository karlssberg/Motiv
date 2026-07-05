using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.BooleanPredicateProposition.PropositionBuilders;

/// <summary>A factory for creating propositions based on a predicate and explanations for true and false conditions.</summary>
/// <param name="predicate">The predicate to use for the specification.</param>
/// <param name="trueBecause">The explanation for when the predicate is true.</param>
/// <param name="falseBecause">The explanation for when the predicate is false.</param>
/// <typeparam name="TModel">The type of the model the proposition is for.</typeparam>
[FluentTarget(typeof(Spec), TerminalMethod = TerminalMethod.None)]
public readonly struct MultiAssertionExplanationWithNamePropositionFactory<TModel>(
    [FluentMethod("Build")]Func<TModel, bool> predicate,
    [FluentMethod("WhenTrue")]string trueBecause,
    [FluentMethod("WhenFalseYield")]Func<TModel, IEnumerable<string>> falseBecause)
{
    /// <summary>
    /// Creates a proposition with explanations for when the condition is true or false. The propositional statement
    /// will be obtained from the .WhenTrue() assertion.
    /// </summary>
    /// <returns>An instance of <see cref="SpecBase{TModel, TMetadata}" />.</returns>
    /// <exception cref="ArgumentException">Thrown when the WhenTrue assertion is null, empty or whitespace (it doubles as the propositional statement).</exception>
    public SpecBase<TModel, string> Create()
    {
        predicate.ThrowIfNull(nameof(predicate));
        trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));
        return new MultiAssertionExplanationProposition<TModel>(
            predicate,
            trueBecause.ToEnumerable().ToFunc<TModel, IEnumerable<string>>(),
            falseBecause,
            new SpecDescription(trueBecause));
    }

    /// <summary>
    /// Creates a proposition with descriptive assertions, but using the supplied proposition to succinctly explain
    /// the decision.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable. Because a name is supplied, the <c>WhenTrue</c>/<c>WhenFalse</c> values are surfaced via <see cref="BooleanResultBase{TMetadata}.Values"/>, not <see cref="BooleanResultBase.Assertions"/>.</remarks>
    /// <returns>An instance of <see cref="SpecBase{TModel, TMetadata}" />.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="statement"/> is null, empty or whitespace.</exception>
    public SpecBase<TModel, string> Create(string statement)
    {
        predicate.ThrowIfNull(nameof(predicate));
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new MultiValueProposition<TModel, string>(
            predicate,
            trueBecause.ToEnumerable().ToFunc<TModel, IEnumerable<string>>(),
            falseBecause,
            new SpecDescription(statement));
    }
}
