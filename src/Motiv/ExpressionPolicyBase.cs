using System.Linq.Expressions;

namespace Motiv;

/// <summary>
/// The base class for policies that retain a recoverable predicate expression tree. A policy resolves
/// to a single assertion or metadata value per evaluation, and this variant additionally allows the
/// composed predicate expression to be recovered via <see cref="ToExpression"/> for use with query
/// providers.
/// </summary>
/// <typeparam name="TModel">The model type that the policy evaluates against.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the policy.</typeparam>
public abstract class ExpressionPolicyBase<TModel, TMetadata> : PolicyBase<TModel, TMetadata>, IExpressionSpec<TModel>
{
    /// <summary>Prevents external instantiation of the <see cref="ExpressionPolicyBase{TModel,TMetadata}"/> class.</summary>
    internal ExpressionPolicyBase()
    {
    }

    /// <summary>Gets the predicate expression tree that this policy represents.</summary>
    /// <returns>The predicate lambda expression describing this policy.</returns>
    public abstract Expression<Func<TModel, bool>> ToExpression();
}
