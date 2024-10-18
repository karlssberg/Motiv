using System.Linq.Expressions;
using Motiv.HigherOrderProposition.PropositionBuilders.Metadata.Spec;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree;

/// <summary>
/// A builder for creating propositions based on a predicate and metadata factories. This is particularly useful
/// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
/// every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the proposition.</typeparam>
/// <typeparam name="TPredicateResult">The return type of the predicate expression.</typeparam>
public readonly ref struct FalseExpressionTreeMultiMetadataFromSpecHigherOrderPropositionBuilder<TModel, TMetadata, TPredicateResult>(
    Expression<Func<TModel, TPredicateResult>> expression,
    Func<IEnumerable<BooleanResult<TModel, string>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<TMetadata>> whenTrue,
    Func<bool, IEnumerable<BooleanResult<TModel, string>>, IEnumerable<BooleanResult<TModel, string>>> causeSelector)
{
    /// <summary>Specifies the metadata to use when the condition is false.</summary>
    /// <param name="whenFalse">The metadata to use when the condition is false.</param>
    /// <returns>An instance of <see cref="MetadataHigherOrderPropositionFactory{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public MultiMetadataFromSpecHigherOrderExpressionTreePropositionFactory<TModel, TMetadata, TPredicateResult> WhenFalse(TMetadata whenFalse) =>
        new(expression,
            higherOrderPredicate,
            whenTrue,
            _ => whenFalse.ToEnumerable(),
            causeSelector);

    /// <summary>Specifies a metadata factory function to use when the condition is false.</summary>
    /// <param name="whenFalse">A function that generates metadata when the condition is false.</param>
    /// <returns>An instance of <see cref="MetadataHigherOrderPropositionFactory{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public MultiMetadataFromSpecHigherOrderExpressionTreePropositionFactory<TModel, TMetadata, TPredicateResult> WhenFalse(
        Func<HigherOrderBooleanResultEvaluation<TModel, string>, TMetadata> whenFalse) =>
        new(expression,
            higherOrderPredicate,
            whenTrue,
            results => whenFalse(results).ToEnumerable(),
            causeSelector);

    /// <summary>Specifies a metadata factory function to use when the condition is false.</summary>
    /// <param name="whenFalse">A function that generates a collection of metadata when the condition is false.</param>
    /// <returns>An instance of <see cref="MetadataHigherOrderPropositionFactory{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public MultiMetadataFromSpecHigherOrderExpressionTreePropositionFactory<TModel, TMetadata, TPredicateResult> WhenFalseYield(
        Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<TMetadata>> whenFalse) =>
        new(expression,
            higherOrderPredicate,
            whenTrue,
            whenFalse,
            causeSelector);
}
