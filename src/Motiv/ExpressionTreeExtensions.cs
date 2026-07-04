using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;

namespace Motiv;

/// <summary>
/// Provides extension methods for working with expression trees.
/// </summary>
public static class ExpressionTreeExtensions
{
    /// <summary>
    /// Converts a predicate expression lambda (i.e., <c>Expression&lt;Func&lt;TModel, bool&gt;&gt;</c>) into
    /// a specification.
    /// </summary>
    /// <param name="expression">The expression to </param>
    /// <typeparam name="TModel"></typeparam>
    /// <returns>An expression-backed proposition whose predicate expression tree can be recovered via <see cref="ExpressionSpecBase{TModel,TMetadata}.ToExpression"/>.</returns>
    public static ExpressionSpecBase<TModel, string> ToSpec<TModel>(this Expression<Func<TModel, bool>> expression) =>
        new ExpressionTreeTransformer<TModel>(expression).Transform();
}
