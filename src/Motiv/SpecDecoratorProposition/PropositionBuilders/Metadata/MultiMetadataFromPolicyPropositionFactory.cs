using Motiv.Generator.Attributes;
using Motiv.Shared;

namespace Motiv.SpecDecoratorProposition.PropositionBuilders.Metadata;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and metadata factories.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TReplacementMetadata">The type of the metadata associated with the proposition.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
[FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
public readonly partial struct MultiMetadataFromPolicyPropositionFactory<TModel, TReplacementMetadata, TMetadata>(
    [MultipleFluentMethods(typeof(PolicyBuildOverloads))]PolicyBase<TModel, TMetadata> spec,
    [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))]Func<TModel, PolicyResultBase<TMetadata>, IEnumerable<TReplacementMetadata>> whenTrue,
    [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<TModel, PolicyResultBase<TMetadata>, IEnumerable<TReplacementMetadata>> whenFalse)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, TReplacementMetadata> Create(string statement) =>
        new PolicyDecoratorMultiMetadataProposition<TModel, TReplacementMetadata, TMetadata>(
            spec,
            whenTrue,
            whenFalse,
            new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement)), spec.Description));
}
