namespace Karlssberg.Motiv.Propositions.FirstOrderSpecBuilders.Metadata;

/// <summary>Represents an interface for building a specification.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata"></typeparam>
public readonly struct DescriptiveMetadataSpecFactory<TModel, TMetadata>(
    Func<TModel, bool> predicate, 
    Func<TModel, TMetadata> whenTrue, 
    Func<TModel, TMetadata> whenFalse)
{
    /// <summary>Provide a human readable explanation for when the condition is false.</summary>
    /// <param name="description">The description of the specification. If not specified, the description of the specification</param>
    /// <returns>A specification base.</returns>
    public SpecBase<TModel, TMetadata> CreateSpec(string description) =>
        new Spec<TModel, TMetadata>(
            description.ThrowIfNullOrWhitespace(nameof(description)),
            predicate,
            whenTrue,
            whenFalse);
}