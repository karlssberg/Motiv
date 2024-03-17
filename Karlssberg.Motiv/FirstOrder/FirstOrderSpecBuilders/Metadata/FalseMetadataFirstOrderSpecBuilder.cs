namespace Karlssberg.Motiv.FirstOrder.FirstOrderSpecBuilders.Metadata;

/// <summary>Represents an interface for asking for a false reason in a specification.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the specification.</typeparam>
public readonly ref struct FalseMetadataFirstOrderSpecBuilder<TModel, TMetadata>(
    Func<TModel, bool> predicate, 
    Func<TModel, TMetadata> whenTrue)
{
    /// <summary>Provide a human readable explanation for when the condition is false.</summary>
    /// <param name="whenFalse">New metadata for when the result is false.</param>
    /// <returns>A specification base.</returns>
    public MetadataWithPropositionFirstOrderSpecFactory<TModel, TMetadata> WhenFalse(TMetadata whenFalse)
    {
        whenFalse.ThrowIfNull(nameof(whenFalse));
        return new MetadataWithPropositionFirstOrderSpecFactory<TModel, TMetadata>(
            predicate,
            whenTrue,
            _ => whenFalse);
    }

    /// <summary>Supply a function that when executed generates a human readable explanation for when the condition is false.</summary>
    /// <param name="whenFalse">The function that evaluates the model and returns new metadata when the result is false.</param>
    /// <returns>A specification base.</returns>
    public MetadataWithPropositionFirstOrderSpecFactory<TModel, TMetadata> WhenFalse(Func<TModel, TMetadata> whenFalse)
    {
        whenFalse.ThrowIfNull(nameof(whenFalse));
        return new MetadataWithPropositionFirstOrderSpecFactory<TModel, TMetadata>(
            predicate,
            whenTrue,
            whenFalse);
    }
}