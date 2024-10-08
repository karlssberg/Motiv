
using System.Linq.Expressions;

namespace Motiv.ExpressionTrees.PropositionBuilders.Metadata;

/// <summary>
/// A builder for creating propositions based on an existing proposition and metadata factories.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the proposition.</typeparam>
public readonly ref struct FalseMetadataPropositionBuilder<TModel, TMetadata>(
    Expression<Func<TModel, bool>> expression,
    Func<TModel, BooleanResultBase<string>, TMetadata> whenTrue)
{
    /// <summary>
    /// Specifies the metadata to use when the condition is false.
    /// </summary>
    /// <param name="whenFalse">The metadata to use when the condition is false.</param>
    /// <returns>A factory for creating propositions based on the supplied proposition and metadata factories.</returns>
    public MetadataPropositionFactory<TModel, TMetadata> WhenFalse(
        TMetadata whenFalse) =>
        new(expression,
            whenTrue,
            (_, _) => whenFalse);

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is false.
    /// </summary>
    /// <param name="whenFalse">A function that generates the metadata when the condition is false.</param>
    /// <returns>A factory for creating propositions based on the supplied proposition and metadata factories.</returns>
    public MetadataPropositionFactory<TModel, TMetadata> WhenFalse(
        Func<TModel, TMetadata> whenFalse) =>
        new(expression,
            whenTrue,
            (model, _) => whenFalse(model));

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is false.
    /// </summary>
    /// <param name="whenFalse">A function that generates the metadata when the condition is false.</param>
    /// <returns>A factory for creating propositions based on the supplied proposition and metadata factories.</returns>
    public MetadataPropositionFactory<TModel, TMetadata> WhenFalse(
        Func<TModel, BooleanResultBase<string>, TMetadata> whenFalse) =>
        new(expression,
            whenTrue,
            whenFalse);

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is false.
    /// </summary>
    /// <param name="whenFalse">A function that generates a collection of metadata when the condition is false.</param>
    /// <returns>A factory for creating specifications based on the supplied proposition and metadata factories.</returns>
    public MultiMetadataPropositionFactory<TModel, TMetadata> WhenFalseYield(
        Func<TModel, BooleanResultBase<string>, IEnumerable<TMetadata>> whenFalse) =>
        new(expression,
            whenTrue.ToEnumerableReturn(),
            whenFalse);
}
