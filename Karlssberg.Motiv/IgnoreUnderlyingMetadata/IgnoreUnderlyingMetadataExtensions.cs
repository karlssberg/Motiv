namespace Karlssberg.Motiv.IgnoreUnderlyingMetadata;

internal static class IgnoreUnderlyingMetadataExtensions
{
    internal static SpecBase<TModel, TMetadata> IgnoreUnderlyingMetadata<TModel, TMetadata, TUnderlyingMetadata>(
        this SpecBase<TModel, TUnderlyingMetadata> underlyingSpec) =>
        new IgnoreUnderlyingMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(underlyingSpec);
}