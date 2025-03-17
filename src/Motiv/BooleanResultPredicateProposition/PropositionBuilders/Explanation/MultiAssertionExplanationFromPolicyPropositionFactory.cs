using Motiv.Generator.Attributes;
using Motiv.Shared;

namespace Motiv.BooleanResultPredicateProposition.PropositionBuilders.Explanation;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and explanation factories.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
public readonly partial struct MultiAssertionExplanationFromPolicyPropositionFactory<TModel, TMetadata>
{
    private readonly Func<TModel, PolicyResultBase<TMetadata>> _predicate;
    private readonly Func<TModel, PolicyResultBase<TMetadata>, IEnumerable<string>> _trueBecause;
    private readonly Func<TModel, PolicyResultBase<TMetadata>, IEnumerable<string>> _falseBecause;

    /// <summary>
    /// A factory for creating propositions based on the supplied proposition and explanation factories.
    /// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers every possibility, so instead it is done on a case-by-case basis.
    /// </summary>
    [FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
    public MultiAssertionExplanationFromPolicyPropositionFactory(
        [MultipleFluentMethods(typeof(PolicyResultBuildOverloads))]Func<TModel, PolicyResultBase<TMetadata>> predicate,
        [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))]Func<TModel, PolicyResultBase<TMetadata>, IEnumerable<string>> trueBecause,
        [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<TModel, PolicyResultBase<TMetadata>, IEnumerable<string>> falseBecause)
    {
        _predicate = predicate;
        _trueBecause = trueBecause;
        _falseBecause = falseBecause;
    }

    /// <summary>
    /// A factory for creating propositions based on the supplied proposition and explanation factories.
    /// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers every possibility, so instead it is done on a case-by-case basis.
    /// </summary>
    [FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
    public MultiAssertionExplanationFromPolicyPropositionFactory(
        [MultipleFluentMethods(typeof(PolicyResultBuildOverloads))]Func<TModel, PolicyResultBase<TMetadata>> predicate,
        [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<TModel, PolicyResultBase<TMetadata>, string> trueBecause,
        [FluentMethod("WhenFalseYield")]Func<TModel, PolicyResultBase<TMetadata>, IEnumerable<string>> falseBecause)
    {
        _predicate = predicate;
        _trueBecause = trueBecause.ToEnumerableReturn();
        _falseBecause = falseBecause;
    }

    /// <summary>
    /// Creates a proposition and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, string> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new PolicyResultPredicateMultiMetadataProposition<TModel, string, TMetadata>(
            _predicate,
            _trueBecause,
            _falseBecause,
            new SpecDescription(statement)
        );
    }
}
