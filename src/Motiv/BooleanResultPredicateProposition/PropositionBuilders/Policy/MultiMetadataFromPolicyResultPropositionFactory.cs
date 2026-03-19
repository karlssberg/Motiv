using Motiv.BooleanResultPredicateProposition.PropositionBuilders.Overloads;
using Motiv.FluentFactory.Attributes;
using Motiv.Shared;

namespace Motiv.BooleanResultPredicateProposition.PropositionBuilders.Policy;

/// <summary>
/// A builder for creating propositions using a predicate function that returns a <see cref="PolicyResultBase{TMetadata}"/>.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TReplacementMetadata">The type of the metadata associated with the proposition.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
public readonly struct MultiMetadataFromPolicyResultPropositionFactory<TModel, TReplacementMetadata, TMetadata>
{
    private readonly Func<TModel, PolicyResultBase<TMetadata>> _spec;
    private readonly Func<TModel, PolicyResultBase<TMetadata>, IEnumerable<TReplacementMetadata>> _whenTrue;
    private readonly Func<TModel, PolicyResultBase<TMetadata>, IEnumerable<TReplacementMetadata>> _whenFalse;

    /// <summary>
    /// A builder for creating propositions using a predicate function that returns a <see cref="PolicyResultBase{TMetadata}"/>.
    /// </summary>
    /// <param name="spec">The predicate function that evaluates the model to a <see cref="PolicyResultBase{TMetadata}"/>.</param>
    /// <param name="whenTrue">The metadata to yield when the predicate is true.</param>
    /// <param name="whenFalse">The metadata to yield when the predicate is false.</param>
    [FluentConstructor(typeof(Motiv.Spec), CreateMethod = CreateMethod.None)]
    public MultiMetadataFromPolicyResultPropositionFactory(
        [MultipleFluentMethods(typeof(PolicyResultBuildOverloads))]Func<TModel, PolicyResultBase<TMetadata>> spec,
        [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))]Func<TModel, PolicyResultBase<TMetadata>, IEnumerable<TReplacementMetadata>> whenTrue,
        [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<TModel, PolicyResultBase<TMetadata>, IEnumerable<TReplacementMetadata>> whenFalse)
    {
        _spec = spec;
        _whenTrue = whenTrue;
        _whenFalse = whenFalse;
    }

    /// <summary>
    /// A builder for creating propositions using a predicate function that returns a <see cref="PolicyResultBase{TMetadata}"/>.
    /// </summary>
    /// <param name="spec">The predicate function that evaluates the model to a <see cref="PolicyResultBase{TMetadata}"/>.</param>
    /// <param name="whenTrue">The metadata to yield when the predicate is true.</param>
    /// <param name="whenFalse">The metadata to yield when the predicate is false.</param>
    [FluentConstructor(typeof(Motiv.Spec), CreateMethod = CreateMethod.None)]
    public MultiMetadataFromPolicyResultPropositionFactory(
        [MultipleFluentMethods(typeof(PolicyResultBuildOverloads))]Func<TModel, PolicyResultBase<TMetadata>> spec,
        [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<TModel, PolicyResultBase<TMetadata>, TReplacementMetadata> whenTrue,
        [FluentMethod("WhenFalseYield", Priority = -1)]Func<TModel, PolicyResultBase<TMetadata>, IEnumerable<TReplacementMetadata>> whenFalse)
    {
        _spec = spec;
        _whenTrue = whenTrue.ToEnumerableReturn();
        _whenFalse = whenFalse;
    }

    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, TReplacementMetadata> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new PolicyResultPredicateMultiValueProposition<TModel, TReplacementMetadata, TMetadata>(
            _spec,
            _whenTrue,
            _whenFalse,
            new SpecDescription(statement) { HasExplicitStatement = true });
    }
}
