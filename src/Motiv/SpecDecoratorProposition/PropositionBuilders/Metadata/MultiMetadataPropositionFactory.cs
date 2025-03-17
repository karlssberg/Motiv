using Motiv.Generator.Attributes;
using Motiv.Shared;

namespace Motiv.SpecDecoratorProposition.PropositionBuilders.Metadata;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and metadata factories.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TReplacementMetadata">The type of the metadata associated with the proposition.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
public readonly partial struct MultiMetadataPropositionFactory<TModel, TReplacementMetadata, TMetadata>
{
    private readonly SpecBase<TModel, TMetadata> _spec;
    private readonly Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<TReplacementMetadata>> _whenTrue;
    private readonly Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<TReplacementMetadata>> _whenFalse;

    /// <summary>
    /// A factory for creating propositions based on the supplied proposition and metadata factories.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TReplacementMetadata">The type of the metadata associated with the proposition.</typeparam>
    /// <typeparam name="TMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
    [FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
    public MultiMetadataPropositionFactory(
        [MultipleFluentMethods(typeof(SpecBuildOverloads))]SpecBase<TModel, TMetadata> spec,
        [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))]Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<TReplacementMetadata>> whenTrue,
        [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<TReplacementMetadata>> whenFalse)
    {
        _spec = spec;
        _whenTrue = whenTrue;
        _whenFalse = whenFalse;
    }

     /// <summary>
    /// A factory for creating propositions based on the supplied proposition and metadata factories.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TReplacementMetadata">The type of the metadata associated with the proposition.</typeparam>
    /// <typeparam name="TMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
    [FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
    public MultiMetadataPropositionFactory(
        [MultipleFluentMethods(typeof(SpecBuildOverloads))]SpecBase<TModel, TMetadata> spec,
        [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<TModel, BooleanResultBase<TMetadata>, TReplacementMetadata> whenTrue,
        [FluentMethod("WhenFalseYield")]Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<TReplacementMetadata>> whenFalse)
    {
        _spec = spec;
        _whenTrue = whenTrue.ToEnumerableReturn();
        _whenFalse = whenFalse;
    }

    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, TReplacementMetadata> Create(string statement) =>
        new SpecDecoratorMultiMetadataProposition<TModel, TReplacementMetadata, TMetadata>(
            _spec,
            _whenTrue,
            _whenFalse,
            new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement)), _spec.Description));
}
