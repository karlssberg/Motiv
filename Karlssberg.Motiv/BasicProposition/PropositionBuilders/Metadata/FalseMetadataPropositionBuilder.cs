namespace Karlssberg.Motiv.BasicProposition.PropositionBuilders.Metadata;

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
    /// <returns>An instance of <see cref="MetadataWithNamePropositionFactory{TModel,TMetadata}" />.</returns>
    public MetadataWithNamePropositionFactory<TModel, TMetadata> WhenFalse(TMetadata whenFalse)
    {
        whenFalse.ThrowIfNull(nameof(whenFalse));
        return new MetadataWithNamePropositionFactory<TModel, TMetadata>(
            predicate,
            whenTrue,
            _ => whenFalse);
    }

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is false.
    /// </summary>
    /// <param name="whenFalse">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="MetadataWithNamePropositionFactory{TModel,TMetadata}" />.</returns>
    public MetadataWithNamePropositionFactory<TModel, TMetadata> WhenFalse(Func<TModel, TMetadata> whenFalse)
    {
        whenFalse.ThrowIfNull(nameof(whenFalse));
        return new MetadataWithNamePropositionFactory<TModel, TMetadata>(
            predicate,
            whenTrue,
            whenFalse);
    }
}