using Motiv.DecoratorProposition.PropositionBuilders.Overloads;
using Motiv.FluentFactory.Attributes;
using Motiv.Shared;

namespace Motiv.DecoratorProposition.PropositionBuilders.Spec;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and explanation factories.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create
/// a proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <param name="spec">The specification to decorate.</param>
/// <param name="trueBecause">The explanation for when the specification is true.</param>
/// <param name="falseBecause">The explanation for when the specification is false.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
[FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
public readonly struct MultiAssertionExplanationPropositionFactory<TModel, TMetadata>(
    [MultipleFluentMethods(typeof(SpecBuildOverloads))]SpecBase<TModel, TMetadata> spec,
    [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))]Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<string>> trueBecause,
    [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<string>> falseBecause)
{
    /// <summary>
    /// Creates a proposition and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, string> Create(string statement) =>
        new SpecDecoratorMultiMetadataProposition<TModel, string, TMetadata>(
            spec,
            trueBecause,
            falseBecause,
            new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement)), spec.Description));
}
