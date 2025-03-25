using Motiv.Generator.Attributes;
using Motiv.HigherOrderProposition.BooleanResultPredicate;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanResultPredicate;

/// <summary>
/// A builder for creating propositions based on a predicate and explanations for true and false conditions.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a
/// proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
[FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
public readonly partial struct MinimalHigherOrderFromBooleanResultPredicatePropositionFactory<TModel, TMetadata>(
    [MultipleFluentMethods(typeof(BooleanResultBuildOverloads))]Func<TModel, BooleanResultBase<TMetadata>> resultResolver,
    [MultipleFluentMethods(typeof(HigherOrderPredicateSpecMethods))]HigherOrderSpecPredicateOperation<TModel, TMetadata> higherOrderOperation)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<IEnumerable<TModel>, TMetadata> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new HigherOrderFromBooleanResultMultiMetadataProposition<TModel, TMetadata, TMetadata>(
            resultResolver,
            higherOrderOperation.HigherOrderPredicate,
            eval => eval.Values,
            eval => eval.Values,
            new SpecDescription(statement),
            higherOrderOperation.CauseSelector);
    }
}
