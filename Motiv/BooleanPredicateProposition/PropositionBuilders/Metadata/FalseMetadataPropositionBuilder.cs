namespace Motiv.BooleanPredicateProposition.PropositionBuilders.Metadata;

/// <summary>
/// A builder for creating propositions based on a predicate and metadata, or for further refining a proposition.
/// </summary>
/// <typeparam name="TModel">The type of the model the proposition is for.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the proposition.</typeparam>
public readonly ref struct FalseMetadataPropositionBuilder<TModel, TMetadata>(
    Func<TModel, bool> predicate,
    Func<TModel, TMetadata> whenTrue)
{
    /// <summary>
    /// Specifies the metadata to use when the condition is false.
    /// </summary>
    /// <param name="whenFalse">The metadata to use when the condition is false.</param>
    /// <returns>An instance of <see cref="MultiMetadataPropositionFactory{TModel,TMetadata}" />.</returns>
    public MetadataPropositionFactory<TModel, TMetadata> WhenFalse(TMetadata whenFalse)
    {
        whenFalse.ThrowIfNull(nameof(whenFalse));
        return new MetadataPropositionFactory<TModel, TMetadata>(
            predicate,
            whenTrue,
            whenFalse.ToFunc<TModel, TMetadata>());
    }

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is false.
    /// </summary>
    /// <param name="whenFalse">A function that generates metadata when the condition is false.</param>
    /// <returns>An instance of <see cref="MultiMetadataPropositionFactory{TModel,TMetadata}" />.</returns>
    public MetadataPropositionFactory<TModel, TMetadata> WhenFalse(Func<TModel, TMetadata> whenFalse)
    {
        whenFalse.ThrowIfNull(nameof(whenFalse));
        return new MetadataPropositionFactory<TModel, TMetadata>(
            predicate,
            whenTrue,
            whenFalse);
    }

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is false.
    /// </summary>
    /// <param name="whenFalse">A function that generates a collection of metadata when the condition is false.</param>
    /// <returns>An instance of <see cref="MultiMetadataPropositionFactory{TModel,TMetadata}" />.</returns>
    public MultiMetadataPropositionFactory<TModel, TMetadata> WhenFalseYield(Func<TModel, IEnumerable<TMetadata>> whenFalse)
    {
        whenFalse.ThrowIfNull(nameof(whenFalse));
        return new MultiMetadataPropositionFactory<TModel, TMetadata>(
            predicate,
            whenTrue.ToEnumerableReturn(),
            whenFalse);
    }
}
