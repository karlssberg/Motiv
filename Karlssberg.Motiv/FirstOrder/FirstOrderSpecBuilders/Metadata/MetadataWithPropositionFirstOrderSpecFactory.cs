namespace Karlssberg.Motiv.FirstOrder.FirstOrderSpecBuilders.Metadata;

/// <summary>
/// A factory for creating specifications based on the supplied predicate and metadata factories.
/// </summary>
/// <typeparam name="TModel">The type of the model the specification is for.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the specification.</typeparam>
public readonly ref struct MetadataWithPropositionFirstOrderSpecFactory<TModel, TMetadata>(
    Func<TModel, bool> predicate,
    Func<TModel, TMetadata> whenTrue,
    Func<TModel, TMetadata> whenFalse)
{
    /// <summary>
    /// Creates a specification and names it with the propositional statement provided.
    /// </summary>
    /// <param name="proposition">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public SpecBase<TModel, TMetadata> Create(string proposition) =>
        new MetadataSpec<TModel, TMetadata>(
            predicate,
            whenTrue,
            whenFalse,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}