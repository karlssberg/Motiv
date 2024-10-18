
using System.Linq.Expressions;

namespace Motiv.ExpressionTreeProposition.PropositionBuilders.Metadata;

/// <summary>
/// A builder for creating propositions based on an existing proposition and metadata factories.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the proposition.</typeparam>
/// <typeparam name="TPredicateResult">The return type of the predicate expression.</typeparam>
public readonly ref struct FalseExpressionTreeMetadataPropositionBuilder<TModel, TMetadata, TPredicateResult>(
    Expression<Func<TModel, TPredicateResult>> expression,
    Func<TModel, BooleanResultBase<string>, TMetadata> whenTrue)
{
    /// <summary>
    /// Specifies the metadata to use when the condition is false.
    /// </summary>
    /// <param name="whenFalse">The metadata to use when the condition is false.</param>
    /// <returns>A factory for creating propositions based on the supplied proposition and metadata factories.</returns>
    public MetadataExpressionTreePropositionFactory<TModel, TMetadata, TPredicateResult> WhenFalse(
        TMetadata whenFalse) =>
        new(expression,
            whenTrue,
            (_, _) => whenFalse);

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is false.
    /// </summary>
    /// <param name="whenFalse">A function that generates the metadata when the condition is false.</param>
    /// <returns>A factory for creating propositions based on the supplied proposition and metadata factories.</returns>
    public MetadataExpressionTreePropositionFactory<TModel, TMetadata, TPredicateResult> WhenFalse(
        Func<TModel, TMetadata> whenFalse) =>
        new(expression,
            whenTrue,
            (model, _) => whenFalse(model));

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is false.
    /// </summary>
    /// <param name="whenFalse">A function that generates the metadata when the condition is false.</param>
    /// <returns>A factory for creating propositions based on the supplied proposition and metadata factories.</returns>
    public MetadataExpressionTreePropositionFactory<TModel, TMetadata, TPredicateResult> WhenFalse(
        Func<TModel, BooleanResultBase<string>, TMetadata> whenFalse) =>
        new(expression,
            whenTrue,
            whenFalse);

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is false.
    /// </summary>
    /// <param name="whenFalse">A function that generates a collection of metadata when the condition is false.</param>
    /// <returns>A factory for creating specifications based on the supplied proposition and metadata factories.</returns>
    public MultiMetadataPropositionExpressionTreeFactory<TModel, TMetadata, TPredicateResult> WhenFalseYield(
        Func<TModel, BooleanResultBase<string>, IEnumerable<TMetadata>> whenFalse) =>
        new(expression,
            whenTrue.ToEnumerableReturn(),
            whenFalse);
}
