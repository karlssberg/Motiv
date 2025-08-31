using Motiv.BooleanResultPredicateProposition.PropositionBuilders.Overloads;
using Motiv.Generator;
using Motiv.Shared;

namespace Motiv.BooleanResultPredicateProposition.PropositionBuilders.Spec;

/// <summary>
/// A builder for creating propositions using a predicate function that returns a <see cref="BooleanResultBase{TMetadata}"/>.
/// </summary>
/// <param name="spec">The predicate function that evaluates the model to a <see cref="BooleanResultBase{TMetadata}"/>.</param>
/// <param name="whenTrue">The metadata factory for the proposition when the predicate is true.</param>
/// <param name="whenFalse">The metadata factory for the proposition when the predicate is false.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TReplacementMetadata">The type of the metadata associated with the proposition.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
[FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
public readonly struct MetadataPropositionFactory<TModel, TReplacementMetadata, TMetadata>(
    [MultipleFluentMethods(typeof(BooleanResultBuildOverloads))]Func<TModel, BooleanResultBase<TMetadata>> spec,
    [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<TModel, BooleanResultBase<TMetadata>, TReplacementMetadata> whenTrue,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<TModel, BooleanResultBase<TMetadata>, TReplacementMetadata> whenFalse)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public PolicyBase<TModel, TReplacementMetadata> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new BooleanResultPredicateProposition<TModel, TReplacementMetadata, TMetadata>(
            spec,
            whenTrue,
            whenFalse,
            new SpecDescription(statement));
    }
}
