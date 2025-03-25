using Motiv.DecoratorProposition.PropositionBuilders.Overloads;
using Motiv.Generator.Attributes;
using Motiv.Shared;

namespace Motiv.DecoratorProposition.PropositionBuilders.Policy;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and metadata factories.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TReplacementMetadata">The type of the metadata associated with the proposition.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
[FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
public readonly partial struct MetadataPolicyFactory<TModel, TReplacementMetadata, TMetadata>(
    [MultipleFluentMethods(typeof(PolicyBuildOverloads))]PolicyBase<TModel, TMetadata> spec,
    [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<TModel, PolicyResultBase<TMetadata>, TReplacementMetadata> whenTrue,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<TModel, PolicyResultBase<TMetadata>, TReplacementMetadata> whenFalse)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public PolicyBase<TModel, TReplacementMetadata> Create(string statement) =>
        new PolicyDecoratorProposition<TModel, TReplacementMetadata, TMetadata>(
            spec,
            whenTrue,
            whenFalse,
            new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement)), spec.Description));
}
