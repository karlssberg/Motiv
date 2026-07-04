using System.Linq.Expressions;
using Motiv.And;
using Motiv.AndAlso;
using Motiv.Not;
using Motiv.Or;
using Motiv.OrElse;
using Motiv.XOr;

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

    /// <summary>
    /// Combines this proposition with another expression-backed proposition using the logical AND operator.
    /// Both operands will be evaluated, regardless of whether the left operand evaluated to <c>false</c>.
    /// </summary>
    /// <param name="spec">The expression-backed proposition to combine with this proposition.</param>
    /// <returns>An expression-backed proposition representing the logical AND of the two propositions.</returns>
    public ExpressionSpecBase<TModel, TMetadata> And(ExpressionSpecBase<TModel, TMetadata> spec) =>
        new ExpressionAndSpec<TModel, TMetadata>(this, spec, this, spec);

    /// <inheritdoc cref="And(ExpressionSpecBase{TModel, TMetadata})"/>
    public ExpressionSpecBase<TModel, TMetadata> And(ExpressionPolicyBase<TModel, TMetadata> spec) =>
        new ExpressionAndSpec<TModel, TMetadata>(this, spec, this, spec);

    /// <summary>
    /// Combines this proposition with another expression-backed proposition using the conditional AND
    /// operator. The right operand is only evaluated if the left operand resolves to <c>true</c>, since a
    /// <c>false</c> left operand means the AND operation cannot return <c>true</c>. This is commonly referred
    /// to as "short-circuiting".
    /// </summary>
    /// <param name="spec">The expression-backed proposition to combine with this proposition.</param>
    /// <returns>An expression-backed proposition representing the conditional AND of the two propositions.</returns>
    public ExpressionSpecBase<TModel, TMetadata> AndAlso(ExpressionSpecBase<TModel, TMetadata> spec) =>
        new ExpressionAndAlsoSpec<TModel, TMetadata>(this, spec, this, spec);

    /// <inheritdoc cref="AndAlso(ExpressionSpecBase{TModel, TMetadata})"/>
    public ExpressionSpecBase<TModel, TMetadata> AndAlso(ExpressionPolicyBase<TModel, TMetadata> spec) =>
        new ExpressionAndAlsoSpec<TModel, TMetadata>(this, spec, this, spec);

    /// <summary>
    /// Combines this proposition with another expression-backed proposition using the logical OR operator.
    /// Both operands will be evaluated, regardless of whether the left operand evaluated to <c>true</c>.
    /// </summary>
    /// <param name="spec">The expression-backed proposition to combine with this proposition.</param>
    /// <returns>An expression-backed proposition representing the logical OR of the two propositions.</returns>
    public ExpressionSpecBase<TModel, TMetadata> Or(ExpressionSpecBase<TModel, TMetadata> spec) =>
        new ExpressionOrSpec<TModel, TMetadata>(this, spec, this, spec);

    /// <inheritdoc cref="Or(ExpressionSpecBase{TModel, TMetadata})"/>
    public ExpressionSpecBase<TModel, TMetadata> Or(ExpressionPolicyBase<TModel, TMetadata> spec) =>
        new ExpressionOrSpec<TModel, TMetadata>(this, spec, this, spec);

    /// <summary>
    /// Combines this proposition with another expression-backed proposition using the conditional OR
    /// operator. The right operand is only evaluated if the left operand resolves to <c>false</c>, since a
    /// <c>true</c> left operand means the OR operation is already satisfied. This is commonly referred to as
    /// "short-circuiting".
    /// </summary>
    /// <param name="spec">The expression-backed proposition to combine with this proposition.</param>
    /// <returns>An expression-backed proposition representing the conditional OR of the two propositions.</returns>
    public ExpressionSpecBase<TModel, TMetadata> OrElse(ExpressionSpecBase<TModel, TMetadata> spec) =>
        new ExpressionOrElseSpec<TModel, TMetadata>(this, spec, this, spec);

    /// <summary>
    /// Combines this proposition with another expression-backed proposition using the logical XOR operator.
    /// Both operands are always evaluated.
    /// </summary>
    /// <param name="spec">The expression-backed proposition to combine with this proposition.</param>
    /// <returns>An expression-backed proposition representing the logical XOR of the two propositions.</returns>
    public ExpressionSpecBase<TModel, TMetadata> XOr(ExpressionSpecBase<TModel, TMetadata> spec) =>
        new ExpressionXOrSpec<TModel, TMetadata>(this, spec, this, spec);

    /// <inheritdoc cref="XOr(ExpressionSpecBase{TModel, TMetadata})"/>
    public ExpressionSpecBase<TModel, TMetadata> XOr(ExpressionPolicyBase<TModel, TMetadata> spec) =>
        new ExpressionXOrSpec<TModel, TMetadata>(this, spec, this, spec);

    /// <summary>Combines two expression-backed propositions using the logical AND operator.</summary>
    /// <param name="left">The left operand of the AND operation.</param>
    /// <param name="right">The right operand of the AND operation.</param>
    /// <returns>An expression-backed proposition representing the logical AND of the two propositions.</returns>
    public static ExpressionSpecBase<TModel, TMetadata> operator &(
        ExpressionPolicyBase<TModel, TMetadata> left,
        ExpressionPolicyBase<TModel, TMetadata> right) =>
        new ExpressionAndSpec<TModel, TMetadata>(left, right, left, right);

    /// <summary>Combines two expression-backed propositions using the logical OR operator.</summary>
    /// <param name="left">The left operand of the OR operation.</param>
    /// <param name="right">The right operand of the OR operation.</param>
    /// <returns>An expression-backed proposition representing the logical OR of the two propositions.</returns>
    public static ExpressionSpecBase<TModel, TMetadata> operator |(
        ExpressionPolicyBase<TModel, TMetadata> left,
        ExpressionPolicyBase<TModel, TMetadata> right) =>
        new ExpressionOrSpec<TModel, TMetadata>(left, right, left, right);

    /// <summary>Combines two expression-backed propositions using the logical XOR operator.</summary>
    /// <param name="left">The left operand of the XOR operation.</param>
    /// <param name="right">The right operand of the XOR operation.</param>
    /// <returns>An expression-backed proposition representing the logical XOR of the two propositions.</returns>
    public static ExpressionSpecBase<TModel, TMetadata> operator ^(
        ExpressionPolicyBase<TModel, TMetadata> left,
        ExpressionPolicyBase<TModel, TMetadata> right) =>
        new ExpressionXOrSpec<TModel, TMetadata>(left, right, left, right);

    /// <summary>Negates this policy. The result remains both a policy and expression-backed.</summary>
    /// <returns>An expression-backed policy representing the logical NOT of this policy.</returns>
    public new ExpressionPolicyBase<TModel, TMetadata> Not() =>
        new ExpressionNotPolicy<TModel, TMetadata>(this);

    /// <summary>Negates an expression-backed policy.</summary>
    /// <param name="policy">The policy to negate.</param>
    /// <returns>An expression-backed policy representing the logical NOT of the policy.</returns>
    public static ExpressionPolicyBase<TModel, TMetadata> operator !(
        ExpressionPolicyBase<TModel, TMetadata> policy) =>
        policy.Not();

    /// <summary>
    /// Creates a policy equivalent to a conditional "OR" of this policy and the alternative policy.
    /// The result remains both a policy and expression-backed.
    /// </summary>
    /// <param name="alternative">The policy to evaluate when this policy is unsatisfied.</param>
    /// <returns>An expression-backed policy representing the conditional OR of the two policies.</returns>
    public ExpressionPolicyBase<TModel, TMetadata> OrElse(ExpressionPolicyBase<TModel, TMetadata> alternative) =>
        new ExpressionOrElsePolicy<TModel, TMetadata>(this, alternative);
}
