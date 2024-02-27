namespace Karlssberg.Motiv.Propositions.FirstOrderSpecBuilders.Metadata;

/// <summary>Represents an interface for building a specification.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata"></typeparam>
public readonly ref struct MetadataWithDescriptionFirstOrderSpecFactory<TModel, TMetadata>(
    Func<TModel, bool> predicate,
    Func<TModel, TMetadata> whenTrue,
    Func<TModel, TMetadata> whenFalse)
{
    /// <summary>Provide a human readable explanation for when the condition is false.</summary>
    /// <param name="proposition">The name of the specification. Preferably this would be in predicate form eg "is even number".</param>
    /// <returns>A specification base.</returns>
    public SpecBase<TModel, TMetadata> CreateSpec(string proposition) =>
        new Spec<TModel, TMetadata>(
            predicate,
            whenTrue,
            whenFalse, proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}