using Motiv.BooleanResultPredicateProposition.PropositionBuilders.Overloads;
using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.BooleanResultPredicateProposition.PropositionBuilders.Spec;

/// <summary>
/// A builder for creating propositions using a predicate function that returns a <see cref="BooleanResultBase{TMetadata}"/>.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TReplacementMetadata">The type of the metadata associated with the proposition.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
public readonly struct MultiMetadataPropositionFactory<TModel, TReplacementMetadata, TMetadata>
{
    private readonly Func<TModel, BooleanResultBase<TMetadata>> _spec;
    private readonly Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<TReplacementMetadata>> _whenTrue;
    private readonly Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<TReplacementMetadata>> _whenFalse;

    /// <summary>
    /// A builder for creating propositions using a predicate function that returns a <see cref="BooleanResultBase{TMetadata}"/>.
    /// </summary>
    /// <param name="spec">The predicate function that evaluates the model to a <see cref="BooleanResultBase{TMetadata}"/>.</param>
    /// <param name="whenTrue">The metadata factory for the proposition when the predicate is true.</param>
    /// <param name="whenFalse">The metadata factory for the proposition when the predicate is false.</param>
    [FluentConstructor(typeof(Motiv.Spec), CreateMethod = CreateMethod.None)]
    public MultiMetadataPropositionFactory(
        [MultipleFluentMethods(typeof(BooleanResultBuildOverloads))]Func<TModel, BooleanResultBase<TMetadata>> spec,
        [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))]Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<TReplacementMetadata>> whenTrue,
        [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<TReplacementMetadata>> whenFalse)
    {
        _spec = spec;
        _whenTrue = whenTrue;
        _whenFalse = whenFalse;
    }

    /// <summary>
    /// A builder for creating propositions using a predicate function that returns a <see cref="BooleanResultBase{TMetadata}"/>.
    /// </summary>
    /// <param name="spec">The predicate function that evaluates the model to a <see cref="BooleanResultBase{TMetadata}"/>.</param>
    /// <param name="whenTrue">The metadata factory for the proposition when the predicate is true.</param>
    /// <param name="whenFalse">The metadata factory for the proposition when the predicate is false.</param>
    [FluentConstructor(typeof(Motiv.Spec), CreateMethod = CreateMethod.None)]
    public MultiMetadataPropositionFactory(
        [MultipleFluentMethods(typeof(BooleanResultBuildOverloads))]Func<TModel, BooleanResultBase<TMetadata>> spec,
        [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<TModel, BooleanResultBase<TMetadata>, TReplacementMetadata> whenTrue,
        [FluentMethod("WhenFalseYield")]Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<TReplacementMetadata>> whenFalse)
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
        return new BooleanResultPredicateMultiValueProposition<TModel, TReplacementMetadata, TMetadata>(
            _spec,
            _whenTrue,
            _whenFalse,
            new SpecDescription(statement) { HasExplicitStatement = true });
    }
}
