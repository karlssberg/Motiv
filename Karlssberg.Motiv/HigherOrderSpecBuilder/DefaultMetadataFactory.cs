using Karlssberg.Motiv.NSatisfied;

namespace Karlssberg.Motiv.HigherOrderSpecBuilder;

internal static class DefaultMetadataFactory
{
    

    internal static MetadataFactory<TModel, TMetadata, TUnderlyingMetadata> GetFactory<TModel, TMetadata, TUnderlyingMetadata>() =>
        new ((_, _) => Enumerable.Empty<TMetadata>());
}