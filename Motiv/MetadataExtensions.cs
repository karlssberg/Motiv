namespace Motiv;

/// <summary>
/// Provides extension methods over custom metadata objects..
/// </summary>
public static class MetadataExtensions
{
    /// <summary>
    /// Gets the metadata from a collection of metadata nodes.
    /// </summary>
    /// <param name="results">The metadata nodes.</param>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <returns>The aggregation of the metadata contained within the supplied metadata nodes.</returns>
    public static IEnumerable<TMetadata> GetMetadata<TMetadata>(
        this IEnumerable<MetadataNode<TMetadata>> results) =>
        results.SelectMany(e => e.Metadata);

    /// <summary>
    /// Gets the metadata from a collection of boolean results.
    /// </summary>
    /// <param name="results">The collection of <see cref="BooleanResultBase{TMetadata}"/> to get metadata from.</param>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <returns>A collection of metadata from the boolean results.</returns>
    public static IEnumerable<TMetadata> GetMetadata<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> results) =>
        results.SelectMany(e => e.Values);

    /// <summary>
    /// Get the metadata from a collection of boolean results that are true.
    /// </summary>
    /// <param name="results">The collection of <see cref="BooleanResultBase{TMetadata}"/> to get metadata from.</param>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <returns>A collection of metadata from the boolean results that are true.</returns>
    public static IEnumerable<TMetadata> GetTrueMetadata<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> results) =>
        results
            .Where(r => r.Satisfied)
            .SelectMany(e => e.Values);

    /// <summary>
    /// Get the metadata from a collection of boolean results that are false.
    /// </summary>
    /// <param name="results">The collection of <see cref="BooleanResultBase{TMetadata}"/> to get metadata from.</param>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <returns>A collection of metadata from the boolean results that are false.</returns>
    public static IEnumerable<TMetadata> GetFalseMetadata<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> results) =>
        results
            .Where(r => !r.Satisfied)
            .SelectMany(e => e.Values);

    internal static IEnumerable<TMetadata> GetRootMetadata<TMetadata>(
        this BooleanResultBase<TMetadata> result) =>
        result.MetadataTier
            .Underlying
            .GetRootMetadata()
            .ElseIfEmpty(result.MetadataTier.Metadata)
            .DistinctWithOrderPreserved();

    private static IEnumerable<TMetadata> GetRootMetadata<TMetadata>(
        this IEnumerable<MetadataNode<TMetadata>> metadataTiers) =>
        metadataTiers.SelectMany(metadataTier => metadataTier
            .Underlying
            .GetRootMetadata()
            .ElseIfEmpty(metadataTier.Metadata));
}