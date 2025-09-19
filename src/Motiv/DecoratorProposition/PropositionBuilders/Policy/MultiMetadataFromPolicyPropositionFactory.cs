using Motiv.DecoratorProposition.PropositionBuilders.Overloads;
using Motiv.FluentFactory.Attributes;
using Motiv.Shared;

namespace Motiv.DecoratorProposition.PropositionBuilders.Policy;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and metadata factories.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TReplacementMetadata">The type of the metadata associated with the proposition.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
public readonly struct MultiMetadataFromPolicyPropositionFactory<TModel, TReplacementMetadata, TMetadata>
{
    private readonly PolicyBase<TModel, TMetadata> _spec;
    private readonly Func<TModel, PolicyResultBase<TMetadata>, IEnumerable<TReplacementMetadata>> _whenTrue;
    private readonly Func<TModel, PolicyResultBase<TMetadata>, IEnumerable<TReplacementMetadata>> _whenFalse;

    /// <summary>
    /// A factory for creating propositions based on the supplied proposition and metadata factories.
    /// </summary>
    /// <param name="spec">The policy to decorate.</param>
    /// <param name="whenTrue">The metadata factory for the proposition when the predicate is true.</param>
    /// <param name="whenFalse">The metadata factory for the proposition when the predicate is false.</param>
    [FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
    public MultiMetadataFromPolicyPropositionFactory(
        [MultipleFluentMethods(typeof(PolicyBuildOverloads))]PolicyBase<TModel, TMetadata> spec,
        [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))]Func<TModel, PolicyResultBase<TMetadata>, IEnumerable<TReplacementMetadata>> whenTrue,
        [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<TModel, PolicyResultBase<TMetadata>, IEnumerable<TReplacementMetadata>> whenFalse)
    {
        _spec = spec;
        _whenTrue = whenTrue;
        _whenFalse = whenFalse;
    }

    /// <summary>
    /// A factory for creating propositions based on the supplied proposition and metadata factories.
    /// </summary>
    /// <param name="spec">The policy to decorate.</param>
    /// <param name="whenTrue">The metadata factory for the proposition when the predicate is true.</param>
    /// <param name="whenFalse">The metadata factory for the proposition when the predicate is false.</param>
    [FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
    public MultiMetadataFromPolicyPropositionFactory(
        [MultipleFluentMethods(typeof(PolicyBuildOverloads))]PolicyBase<TModel, TMetadata> spec,
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
    public SpecBase<TModel, TReplacementMetadata> Create(string statement) =>
        new PolicyDecoratorMultiMetadataProposition<TModel, TReplacementMetadata, TMetadata>(
            _spec,
            _whenTrue,
            _whenFalse,
            new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement)), _spec.Description));
}
