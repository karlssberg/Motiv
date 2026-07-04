using System.Linq.Expressions;

namespace Motiv;

/// <summary>
/// The base class for propositions that retain a recoverable predicate expression tree. Composing
/// instances of this type (or <see cref="ExpressionPolicyBase{TModel,TMetadata}"/>) with the logical
/// operators yields propositions that are themselves expression-backed, so the composed expression
/// can be recovered via <see cref="ToExpression"/> and used with query providers.
/// </summary>
/// <typeparam name="TModel">The model type that the proposition evaluates against.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the proposition.</typeparam>
public abstract class ExpressionSpecBase<TModel, TMetadata> : SpecBase<TModel, TMetadata>, IExpressionSpec<TModel>
{
    /// <summary>Prevents external instantiation of the <see cref="ExpressionSpecBase{TModel,TMetadata}"/> class.</summary>
    internal ExpressionSpecBase()
    {
    }

    /// <summary>Gets the predicate expression tree that this proposition represents.</summary>
    /// <returns>The predicate lambda expression describing this proposition.</returns>
    public abstract Expression<Func<TModel, bool>> ToExpression();
}
