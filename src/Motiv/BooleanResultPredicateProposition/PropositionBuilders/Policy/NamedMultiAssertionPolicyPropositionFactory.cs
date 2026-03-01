using Motiv.FluentFactory.Attributes;
using Motiv.Shared;

namespace Motiv.BooleanResultPredicateProposition.PropositionBuilders.Policy;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and explanation factories.
/// </summary>
/// <param name="predicate">The predicate to use for the specification.</param>
/// <param name="trueBecause">The explanation for when the predicate is true.</param>
/// <param name="falseBecause">The explanation for when the predicate is false.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
[FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
public readonly struct NamedMultiAssertionPolicyPropositionFactory<TModel, TMetadata>(
    [MultipleFluentMethods(typeof(PolicyResultBuildOverloads))]Func<TModel, PolicyResultBase<TMetadata>> predicate,
    [FluentMethod("WhenTrue")]string trueBecause,
    [FluentMethod("WhenFalseYield")]Func<TModel, PolicyResultBase<TMetadata>, IEnumerable<string>> falseBecause)
{
    private Func<TModel, PolicyResultBase<TMetadata>, IEnumerable<string>> TrueBecauseFunc =>
        trueBecause
            .ToEnumerable()
            .ToFunc<TModel, PolicyResultBase<TMetadata>, IEnumerable<string>>();

    /// <summary>
    /// Creates a proposition and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, string> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new PolicyResultPredicateMultiValueProposition<TModel, string, TMetadata>(
            predicate,
            TrueBecauseFunc,
            falseBecause,
            new SpecDescription(statement) { HasExplicitStatement = true });
    }

    /// <summary>
    /// Creates a proposition with explanations for when the condition is true or false. The propositional statement
    /// will be obtained from the .WhenTrue() assertion.
    /// </summary>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, string> Create() =>
        new PolicyResultPredicateMultiValueProposition<TModel, string, TMetadata>(
            predicate,
            TrueBecauseFunc,
            falseBecause,
            new SpecDescription(trueBecause));
}
