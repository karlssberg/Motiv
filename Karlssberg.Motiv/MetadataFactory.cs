namespace Karlssberg.Motiv;

public readonly struct MetadataFactory<TModel, TMetadata, TUnderlyingMetadata>(
    Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>>
        metadataFactoryFn)
{
    public MetadataFactory() : this(DefaultFactory)
    {
    }

    public IEnumerable<TMetadata> Create(bool isSatisfied,
        IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>> results) =>
        metadataFactoryFn(isSatisfied, results);

    public static implicit operator MetadataFactory<TModel, TMetadata, TUnderlyingMetadata>(
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>>
            metadataFactoryFn) => new(metadataFactoryFn);

    public static implicit operator MetadataFactory<TModel, TMetadata, TUnderlyingMetadata>(
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, TMetadata>
            metadataFactoryFn) => new((isSatisfied, results) => new[] { metadataFactoryFn(isSatisfied, results) });

    private static IEnumerable<TMetadata> DefaultFactory(
        bool isSatisfied,
        IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>> underlyingResults) =>
        underlyingResults.Select(result => result.GetMetadata())
            .SelectMany(metadata => metadata)
            .OfType<TMetadata>();
}