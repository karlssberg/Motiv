namespace Motiv;

/// <summary>
/// Provides extension methods for using expression-backed propositions with
/// <see cref="IQueryable{T}"/> sources.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Filters a sequence of values using the proposition's underlying predicate expression tree.
    /// The expression is passed to the query provider verbatim, so providers such as EF Core can
    /// translate it (e.g. to SQL) rather than evaluating it client-side.
    /// </summary>
    /// <param name="source">The queryable source to filter.</param>
    /// <param name="spec">The expression-backed proposition to filter with.</param>
    /// <typeparam name="TModel">The element type of the queryable source.</typeparam>
    /// <returns>A queryable filtered by the proposition's predicate expression.</returns>
    public static IQueryable<TModel> Where<TModel>(
        this IQueryable<TModel> source,
        IExpressionSpec<TModel> spec) =>
        Queryable.Where(source, spec.ToExpression());
}
