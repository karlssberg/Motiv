using Motiv.BooleanPredicateProposition.PropositionBuilders.Overloads;
using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.BooleanPredicateProposition.PropositionBuilders;

/// <summary>A factory for creating asynchronous propositions based on an async predicate and explanations for true and false conditions.</summary>
/// <param name="predicate">The async predicate function that evaluates the model to a boolean value.</param>
/// <param name="trueBecause">The explanation for when the predicate is true.</param>
/// <param name="falseBecause">The explanation for when the predicate is false.</param>
/// <typeparam name="TModel">The type of the model the proposition is for.</typeparam>
[FluentTarget(typeof(Spec), TerminalMethod = TerminalMethod.None)]
public readonly struct AsyncExplanationWithNamePropositionFactory<TModel>(
    [MultipleFluentMethods(typeof(BuildAsyncOverloads))]Func<TModel, CancellationToken, ValueTask<bool>> predicate,
    [FluentMethod("WhenTrue")]string trueBecause,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<TModel, string> falseBecause)
{
    /// <summary>
    /// Creates an asynchronous proposition with explanations for when the condition is true or false. The
    /// propositional statement will be obtained from the .WhenTrue() assertion.
    /// </summary>
    /// <returns>An instance of <see cref="AsyncPolicyBase{TModel, TMetadata}" />.</returns>
    /// <exception cref="ArgumentException">Thrown when the WhenTrue assertion is null, empty or whitespace (it doubles as the propositional statement).</exception>
    public AsyncPolicyBase<TModel, string> Create()
    {
        predicate.ThrowIfNull(nameof(predicate));
        trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));
        return new AsyncExplanationProposition<TModel>(
            predicate,
            trueBecause.ToFunc<TModel, string>(),
            falseBecause,
            new SpecDescription(trueBecause));
    }

    /// <summary>
    /// Creates an asynchronous proposition with descriptive assertions, but using the supplied proposition to
    /// succinctly explain the decision.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable. Because a name is supplied, the <c>WhenTrue</c>/<c>WhenFalse</c> values are surfaced via <see cref="BooleanResultBase{TMetadata}.Values"/>, not <see cref="BooleanResultBase.Assertions"/>.</remarks>
    /// <returns>An instance of <see cref="AsyncPolicyBase{TModel, TMetadata}" />.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="statement"/> is null, empty or whitespace.</exception>
    public AsyncPolicyBase<TModel, string> Create(string statement)
    {
        predicate.ThrowIfNull(nameof(predicate));
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new AsyncProposition<TModel, string>(
            predicate,
            trueBecause.ToFunc<TModel, string>(),
            falseBecause,
            new SpecDescription(statement));
    }
}
