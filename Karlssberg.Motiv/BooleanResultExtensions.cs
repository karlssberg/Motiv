using Karlssberg.Motiv.Visitors;

namespace Karlssberg.Motiv;

/// <summary>
/// Provides extension methods for the BooleanResultBase class. These methods dispatch to a visitor that will
/// aggregate metadata from the underlying result tree.
/// </summary>
public static class BooleanResultExtensions
{
    /// <summary>Retrieves metadata from the BooleanResultBase instance using a default metadata visitor.</summary>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="booleanResultBase">The BooleanResultBase instance.</param>
    /// <returns>A distinct collection of metadata.</returns>
    public static IEnumerable<TMetadata> GetMetadata<TMetadata>(
        this BooleanResultBase<TMetadata> booleanResultBase) =>
        booleanResultBase
            .GetMetadata(new DefaultMetadataVisitor<TMetadata>())
            .Distinct();

    /// <summary>Retrieves metadata from the BooleanResultBase instance using a specified metadata visitor.</summary>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <typeparam name="TVisitor">The type of the visitor.</typeparam>
    /// <param name="booleanResultBase">The BooleanResultBase instance.</param>
    /// <param name="visitor">The visitor to use for metadata retrieval.</param>
    /// <returns>A collection of metadata.</returns>
    public static IEnumerable<TMetadata> GetMetadata<TMetadata, TVisitor>(
        this BooleanResultBase<TMetadata> booleanResultBase,
        TVisitor visitor)
        where TVisitor : DefaultMetadataVisitor<TMetadata> =>
        visitor.Visit(booleanResultBase);

    /// <summary>Retrieves root causes from the BooleanResultBase instance.</summary>
    /// <param name="booleanResultBase">The BooleanResultBase instance.</param>
    /// <returns>A distinct collection of superficial reasons.</returns>
    public static IEnumerable<string> GetDeepReasons(
        this BooleanResultBase<string> booleanResultBase) =>
        booleanResultBase
            .GetMetadata(new DeepMetadataVisitor<string>())
            .Distinct();
    
    internal static IEnumerable<TModel> GetModelsWhere<TModel, TMetadata>(
        this IEnumerable<BooleanResultWithModel<TModel, TMetadata>> underlyingResults, 
        Func<BooleanResultWithModel<TModel, TMetadata>, bool> filter)
    {
        return underlyingResults
            .Where(filter)
            .Select(result => result.Model);
    }
}