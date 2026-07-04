using System.Linq.Expressions;
using Motiv.And;

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

    /// <summary>
    /// Combines this proposition with another expression-backed proposition using the logical AND
    /// operator. The result is itself expression-backed.
    /// </summary>
    /// <param name="spec">The expression-backed proposition to combine with this proposition.</param>
    /// <returns>An expression-backed proposition representing the logical AND of the two propositions.</returns>
    public ExpressionSpecBase<TModel, TMetadata> And(ExpressionSpecBase<TModel, TMetadata> spec) =>
        new ExpressionAndSpec<TModel, TMetadata>(this, spec, this, spec);

    /// <inheritdoc cref="And(ExpressionSpecBase{TModel, TMetadata})"/>
    public ExpressionSpecBase<TModel, TMetadata> And(ExpressionPolicyBase<TModel, TMetadata> spec) =>
        new ExpressionAndSpec<TModel, TMetadata>(this, spec, this, spec);

    /// <summary>Combines two expression-backed propositions using the logical AND operator.</summary>
    /// <param name="left">The left operand of the AND operation.</param>
    /// <param name="right">The right operand of the AND operation.</param>
    /// <returns>An expression-backed proposition representing the logical AND of the two propositions.</returns>
    public static ExpressionSpecBase<TModel, TMetadata> operator &(
        ExpressionSpecBase<TModel, TMetadata> left,
        ExpressionSpecBase<TModel, TMetadata> right) =>
        left.And(right);

    /// <inheritdoc cref="op_BitwiseAnd(ExpressionSpecBase{TModel, TMetadata}, ExpressionSpecBase{TModel, TMetadata})"/>
    public static ExpressionSpecBase<TModel, TMetadata> operator &(
        ExpressionSpecBase<TModel, TMetadata> left,
        ExpressionPolicyBase<TModel, TMetadata> right) =>
        left.And(right);

    /// <inheritdoc cref="op_BitwiseAnd(ExpressionSpecBase{TModel, TMetadata}, ExpressionSpecBase{TModel, TMetadata})"/>
    public static ExpressionSpecBase<TModel, TMetadata> operator &(
        ExpressionPolicyBase<TModel, TMetadata> left,
        ExpressionSpecBase<TModel, TMetadata> right) =>
        new ExpressionAndSpec<TModel, TMetadata>(left, right, left, right);
}
