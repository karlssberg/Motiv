using Motiv.Generator.Attributes;
using Motiv.Shared;

namespace Motiv.BooleanResultPredicateProposition.PropositionBuilders.Metadata;

/// <summary>
/// A builder for creating propositions using a predicate function that returns a <see cref="BooleanResultBase{TMetadata}"/>.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TReplacementMetadata">The type of the metadata associated with the proposition.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
[FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
public readonly partial struct MultiMetadataPropositionFactory<TModel, TReplacementMetadata, TMetadata>(
    [MultipleFluentMethods(typeof(BooleanResultBuildOverloads))]Func<TModel, BooleanResultBase<TMetadata>> spec,
    [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))]Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<TReplacementMetadata>> whenTrue,
    [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<TReplacementMetadata>> whenFalse)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, TReplacementMetadata> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new BooleanResultPredicateMultiMetadataProposition<TModel, TReplacementMetadata, TMetadata>(
            spec,
            whenTrue,
            whenFalse,
            new SpecDescription(statement));
    }
}
