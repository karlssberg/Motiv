using System.Linq.Expressions;
using Motiv.ExpressionTrees;

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
    /// <returns></returns>
    public static SpecBase<TModel, string> ToSpec<TModel>(this Expression<Func<TModel, bool>> expression) =>
        new ExpressionTreeTransformer<TModel>().Transform(expression);
}
