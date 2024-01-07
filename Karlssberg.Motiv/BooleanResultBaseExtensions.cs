namespace Karlssberg.Motiv;

public static class BooleanResultBaseExtensions
{
    /// <summary>
    /// Retrieves the insights from a <see cref="BooleanResultBase{TMetadata}"/> using the default insights visitor.
    /// </summary>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="booleanResultBase">The <see cref="BooleanResultBase{TMetadata}"/> instance.</param>
    /// <returns>An <see cref="IEnumerable{TMetadata}"/> containing the insights.</returns>
    public static IEnumerable<TMetadata> GetInsights<TMetadata>(this BooleanResultBase<TMetadata> booleanResultBase) =>
        booleanResultBase
            .GetInsights(new DefaultInsightsVisitor<TMetadata>())
            .Distinct();

    /// <summary>
    /// Retrieves the insights from a <see cref="BooleanResultBase{TMetadata}"/> using a custom insights visitor.
    /// </summary>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <typeparam name="TVisitor">The type of the insights visitor.</typeparam>
    /// <param name="booleanResultBase">The <see cref="BooleanResultBase{TMetadata}"/> instance.</param>
    /// <param name="visitor">The custom insights visitor.</param>
    /// <returns>An <see cref="IEnumerable{TMetadata}"/> containing the insights.</returns>
    public static IEnumerable<TMetadata> GetInsights<TMetadata, TVisitor>(
        this BooleanResultBase<TMetadata> booleanResultBase,
        TVisitor visitor)
        where TVisitor : DefaultInsightsVisitor<TMetadata> =>
        visitor.Visit(booleanResultBase);
}