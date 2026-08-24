using Motiv.Shared;
using Motiv.Traversal;

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
    public static IEnumerable<TMetadata> GetValues<TMetadata>(
        this IEnumerable<MetadataNode<TMetadata>> results) =>
        results.SelectMany(e => e.Metadata);

    /// <summary>
    /// Gets the metadata from a collection of boolean results.
    /// </summary>
    /// <param name="results">The collection of <see cref="BooleanResultBase{TMetadata}"/> to get metadata from.</param>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <returns>A collection of metadata from the boolean results.</returns>
    public static IEnumerable<TMetadata> GetValues<TMetadata>(
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

    internal static IEnumerable<TMetadata> GetRootValues<TMetadata>(
        this BooleanResultBase<TMetadata> result) =>
        RootValuesOf(result.MetadataTier.Underlying)
            .ElseIfEmpty(result.MetadataTier.Metadata)
            .DistinctWithOrderPreserved();

    /// <remarks>
    /// The metadata tier is a tree in its own right, so this walk folds rather than recurses for the
    /// same reason the assertion walks do (Spec 3A / ticket 19).
    /// </remarks>
    private static IEnumerable<TMetadata> RootValuesOf<TMetadata>(IEnumerable<MetadataNode<TMetadata>> tiers)
    {
        var memo = new Dictionary<MetadataNode<TMetadata>, TMetadata[]>(
            ReferenceEqualityComparer<MetadataNode<TMetadata>>.Instance);

        var values = new List<TMetadata>();

        foreach (var tier in tiers)
            values.AddRange(PostOrderFold.Fold(
                tier,
                node => node.Underlying as IReadOnlyList<MetadataNode<TMetadata>> ?? node.Underlying.ToArray(),
                (node, folded) =>
                {
                    var rootValues = new List<TMetadata>();
                    for (var i = 0; i < folded.Count; i++)
                        rootValues.AddRange(folded[i]);

                    return rootValues.Count == 0
                        ? node.Metadata.ToArray()
                        : rootValues.ToArray();
                },
                node => memo.TryGetValue(node, out var folded) ? folded : null,
                (node, folded) => memo[node] = folded));

        return values;
    }
}
