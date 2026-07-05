using Motiv.DecoratorProposition.PropositionBuilders.Overloads;
using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.DecoratorProposition.PropositionBuilders.Spec;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and explanation factories.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
public readonly struct NamedPolicyPropositionFactory<TModel, TMetadata>
{
    private readonly SpecBase<TModel, TMetadata> _spec;
    private readonly string _trueBecause;
    private readonly Func<TModel, BooleanResultBase<TMetadata>, string> _falseBecause;

    /// <summary>
    /// A factory for creating propositions based on the supplied proposition and explanations.
    /// </summary>
    /// <param name="spec">The specification to decorate.</param>
    /// <param name="trueBecause">The explanation for when the policy is true.</param>
    /// <param name="falseBecause">The explanation for when the policy is false.</param>
    [FluentTarget(typeof(Motiv.Spec), TerminalMethod = TerminalMethod.None)]
    public NamedPolicyPropositionFactory(
        [MultipleFluentMethods(typeof(SpecBuildOverloads))]SpecBase<TModel, TMetadata> spec,
        [FluentMethod("WhenTrue")]string trueBecause,
        [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<TModel, BooleanResultBase<TMetadata>, string> falseBecause)
    {
        _spec = spec;
        _trueBecause = trueBecause;
        _falseBecause = falseBecause;
    }

    /// <summary>
    /// Creates a proposition with explanations for when the condition is true or false. The propositional statement
    /// will be obtained from the .WhenTrue() assertion.
    /// </summary>
    /// <returns>A proposition for the model.</returns>
    /// <exception cref="ArgumentException">Thrown when the WhenTrue assertion is null, empty or whitespace (it doubles as the propositional statement).</exception>
    public PolicyBase<TModel, string> Create()
    {
        _trueBecause.ThrowIfNullOrWhitespace(nameof(_trueBecause));
        return new SpecDecoratorWithSingleTrueAssertionProposition<TModel, TMetadata>(
            _spec,
            _trueBecause,
            _falseBecause,
            new SpecDescription(_trueBecause, _spec.Description));
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
        new SpecDecoratorProposition<TModel, string, TMetadata>(
            _spec,
            _trueBecause.ToFunc<TModel, BooleanResultBase<TMetadata>, string>(),
            _falseBecause,
            new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement)), _spec.Description));
}
