using Motiv.DecoratorProposition.PropositionBuilders.Overloads;
using Motiv.FluentFactory.Attributes;
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
    [FluentConstructor(typeof(Motiv.Spec), CreateMethod = CreateMethod.None)]
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
    public PolicyBase<TModel, string> Create() =>
        new SpecDecoratorWithSingleTrueAssertionProposition<TModel,TMetadata>(
            _spec,
            _trueBecause,
            _falseBecause);

    /// <summary>
    /// Creates a proposition with descriptive assertions, but using the supplied proposition to succinctly explain
    /// the decision.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public PolicyBase<TModel, string> Create(string statement) =>
        new SpecDecoratorWithSingleTrueAssertionProposition<TModel, TMetadata>(
            _spec,
            _trueBecause,
            _falseBecause,
            statement.ThrowIfNullOrWhitespace(nameof(statement)));
}
