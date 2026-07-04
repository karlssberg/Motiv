using System.Linq.Expressions;

namespace Motiv;

/// <summary>
/// Represents a proposition that retains a recoverable predicate expression tree, suitable for use
/// with query providers (e.g. <see cref="IQueryable{T}"/> translation).
/// </summary>
/// <typeparam name="TModel">The model type that the proposition evaluates against.</typeparam>
public interface IExpressionSpec<TModel>
{
    /// <summary>Gets the predicate expression tree that this proposition represents.</summary>
    /// <returns>The predicate lambda expression describing this proposition.</returns>
    Expression<Func<TModel, bool>> ToExpression();
}
