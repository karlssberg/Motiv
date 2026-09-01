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
        RootValuesOf(result.MetadataTier).DistinctWithOrderPreserved();

    /// <remarks>
    /// The metadata tier is a tree in its own right, so this walk folds rather than recurses for the
    /// same reason the assertion walks do (Spec 3A / ticket 19). The deepest tier is a property of a
    /// branch, so it descends <see cref="MetadataNode{TMetadata}.Branches" /> rather than
    /// <see cref="MetadataNode{TMetadata}.Underlying" /> — see that property's remarks for why the
    /// latter cannot answer the question (ticket #189).
    /// <para>
    /// The fallback is on the branches having <i>contributed</i> nothing rather than on there being
    /// no branches, which is what its assertion twin <c>CombineRootAssertions</c> does. The two forms
    /// differ only for a branch whose whole subtree yields no metadata, and falling back only for a
    /// childless branch would drop that branch's own value — #189 one level up.
    /// </para>
    /// </remarks>
    private static TMetadata[] RootValuesOf<TMetadata>(MetadataNode<TMetadata> tier)
    {
        var memo = new Dictionary<MetadataNode<TMetadata>, TMetadata[]>(
            ReferenceEqualityComparer<MetadataNode<TMetadata>>.Instance);

        return PostOrderFold.Fold(
            tier,
            node => node.Branches,
            (node, foldedBranches) =>
            {
                var branchValues = foldedBranches.Flatten();

                return branchValues.Length == 0
                    ? node.Metadata.ToArray()
                    : branchValues;
            },
            node => memo.TryGetValue(node, out var folded) ? folded : null,
            (node, folded) => memo[node] = folded);
    }
}
