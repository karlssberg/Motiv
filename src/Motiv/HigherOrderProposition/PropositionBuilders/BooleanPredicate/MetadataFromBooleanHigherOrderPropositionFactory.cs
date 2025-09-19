using Motiv.FluentFactory.Attributes;
using Motiv.HigherOrderProposition.BooleanPredicate;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.BooleanPredicate;

/// <summary>
/// A factory for creating propositions based on a predicate and metadata factories. This is particularly useful
/// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
/// every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <param name="resultResolver">The predicate to use for the specification.</param>
/// <param name="higherOrderOperation">The higher-order operation to use for the specification.</param>
/// <param name="whenTrue">The metadata factory for when the predicate is true.</param>
/// <param name="whenFalse">The metadata factory for when the predicate is false.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the specification.</typeparam>
[FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
public readonly struct MetadataFromBooleanHigherOrderPropositionFactory<TModel, TMetadata>(
    [FluentMethod("Build")]Func<TModel, bool> resultResolver,
    [MultipleFluentMethods(typeof(HigherOrderBooleanPredicateSpecMethods))]HigherOrderSpecBooleanPredicateOperation<TModel> higherOrderOperation,
    [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenTrue,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenFalse)
{
    /// <summary>Creates a specification and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public PolicyBase<IEnumerable<TModel>, TMetadata> Create(string statement)
    {
        resultResolver.ThrowIfNull(nameof(resultResolver));
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new HigherOrderFromBooleanPredicateProposition<TModel,TMetadata>(
            resultResolver,
            higherOrderOperation.HigherOrderPredicate,
            whenTrue,
            whenFalse,
            new SpecDescription(statement),
            higherOrderOperation.CauseSelector);
    }
}
