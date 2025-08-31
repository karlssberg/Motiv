using Motiv.BooleanPredicateProposition.PropositionBuilders.Overloads;
using Motiv.FluentFactory.Generator;
using Motiv.Shared;

namespace Motiv.BooleanPredicateProposition.PropositionBuilders;

/// <summary>
/// A factory for creating propositions based on the supplied predicate and metadata factories.
/// </summary>
/// <param name="predicate">The predicate to use for the specification.</param>
/// <param name="whenTrue">The metadata factory for the proposition when the predicate is true.</param>
/// <param name="whenFalse">The metadata factory for the proposition when the predicate is false.</param>
/// <typeparam name="TModel">The type of the model the proposition is for.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the proposition.</typeparam>
[FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
public readonly struct MetadataPropositionFactory<TModel, TMetadata>(
    [FluentMethod("Build")]Func<TModel, bool> predicate,
    [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<TModel, TMetadata> whenTrue,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<TModel, TMetadata> whenFalse)
{
    /// <summary>
    /// Creates a proposition and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public PolicyBase<TModel, TMetadata> Create(string statement)
    {
        predicate.ThrowIfNull(nameof(predicate));
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new Proposition<TModel, TMetadata>(
            predicate,
            whenTrue,
            whenFalse,
            new SpecDescription(statement));
    }
}
