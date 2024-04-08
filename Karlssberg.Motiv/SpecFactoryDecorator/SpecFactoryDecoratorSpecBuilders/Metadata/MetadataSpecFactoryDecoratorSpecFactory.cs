namespace Karlssberg.Motiv.SpecFactoryDecorator.SpecFactoryDecoratorSpecBuilders.Metadata;

/// <summary>
/// Represents a factory for creating propositions based on a predicate and metadata factories. This is
/// particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a
/// proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the proposition.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
public readonly ref struct MetadataSpecFactoryDecoratorSpecFactory<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="proposition">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, TMetadata> Create(string proposition) =>
        new SpecFactoryDecoratorMultiMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
            specPredicate,
            whenTrue,
            whenFalse,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}