using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;
using Motiv.Generator.Attributes;
using Motiv.HigherOrderProposition.ExpressionTree;
using Motiv.Shared;
using SpecDescription = Motiv.ExpressionTreeProposition.SpecDescription;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree;

/// <summary>
/// A factory for creating propositions based on a predicate and metadata factories. This is particularly useful
/// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
/// every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the specification.</typeparam>
/// <typeparam name="TPredicateResult">The return type of the predicate expression.</typeparam>
[FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
public readonly partial struct MetadataHigherOrderExpressionTreePropositionFactory<TModel, TMetadata, TPredicateResult>(
    [FluentMethod("From")]Expression<Func<TModel, TPredicateResult>> expression,
    [MultipleFluentMethods(typeof(HigherOrderPredicateSpecMethods))]HigherOrderSpecPredicateOperation<TModel, string> higherOrderOperation,
    [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<HigherOrderBooleanResultEvaluation<TModel, string>, TMetadata> whenTrue,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<HigherOrderBooleanResultEvaluation<TModel, string>, TMetadata> whenFalse)
{
    /// <summary>Creates a specification and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public PolicyBase<IEnumerable<TModel>, TMetadata> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new HigherOrderFromBooleanResultMetadataExpressionTreeProposition<TModel, TMetadata, TPredicateResult>(
            expression,
            higherOrderOperation.HigherOrderPredicate,
            whenTrue,
            whenFalse,
            new SpecDescription(statement),
            higherOrderOperation.CauseSelector);
    }

    /// <summary>Creates a specification.</summary>
    /// <returns>A specification for the model.</returns>
    public PolicyBase<IEnumerable<TModel>, TMetadata> Create()
    {
        return new HigherOrderFromBooleanResultMetadataExpressionTreeProposition<TModel, TMetadata, TPredicateResult>(
            expression,
            higherOrderOperation.HigherOrderPredicate,
            whenTrue,
            whenFalse,
            new ExpressionTreeDescription<TModel, TPredicateResult>(expression),
            higherOrderOperation.CauseSelector);
    }
}
