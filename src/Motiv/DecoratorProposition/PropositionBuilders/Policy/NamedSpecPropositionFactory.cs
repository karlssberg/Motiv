using Motiv.DecoratorProposition.PropositionBuilders.Overloads;
using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.DecoratorProposition.PropositionBuilders.Policy;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and explanation factories.
/// </summary>
/// <param name="policy">The policy to use for the proposition.</param>
/// <param name="trueBecause">The explanation for when the predicate is true.</param>
/// <param name="falseBecause">The explanation for when the predicate is false.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
[FluentTarget(typeof(Motiv.Spec), TerminalMethod = TerminalMethod.None)]
public readonly struct NamedSpecPropositionFactory<TModel, TMetadata>(
    [MultipleFluentMethods(typeof(PolicyBuildOverloads))]PolicyBase<TModel, TMetadata> policy,
    [FluentMethod("WhenTrue")]string trueBecause,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<TModel, PolicyResultBase<TMetadata>, string> falseBecause)
{
    /// <summary>
    /// Creates a proposition with explanations for when the condition is true or false. The propositional statement
    /// will be obtained from the .WhenTrue() assertion.
    /// </summary>
    /// <returns>A proposition for the model.</returns>
    /// <exception cref="ArgumentException">Thrown when the WhenTrue assertion is null, empty or whitespace (it doubles as the propositional statement).</exception>
    public PolicyBase<TModel, string> Create()
    {
        trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));
        return new PolicyDecoratorWithSingleTrueAssertionProposition<TModel, TMetadata>(
            policy,
            trueBecause,
            falseBecause,
            new SpecDescription(trueBecause, policy.Description));
    }

    /// <summary>
    /// Creates a proposition with descriptive assertions, but using the supplied proposition to succinctly explain
    /// the decision.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable. Because a name is supplied, the <c>WhenTrue</c>/<c>WhenFalse</c> values are surfaced via <see cref="BooleanResultBase{TMetadata}.Values"/>, not <see cref="BooleanResultBase.Assertions"/>.</remarks>
    /// <returns>A proposition for the model.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="statement"/> is null, empty or whitespace.</exception>
    public PolicyBase<TModel, string> Create(string statement) =>
        new PolicyDecoratorProposition<TModel, string, TMetadata>(
            policy,
            trueBecause.ToFunc<TModel, PolicyResultBase<TMetadata>, string>(),
            falseBecause,
            new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement)), policy.Description));
}
