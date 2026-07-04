using System.Linq.Expressions;
using Motiv.And;
using Motiv.AndAlso;
using Motiv.ExpressionTreeProposition;
using Motiv.Not;
using Motiv.Or;
using Motiv.OrElse;
using Motiv.XOr;

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

    private SpecBase<TModel, string>? _explanationExpressionSpec;

    /// <summary>Gets the predicate expression tree that this proposition represents.</summary>
    /// <returns>The predicate lambda expression describing this proposition.</returns>
    public abstract Expression<Func<TModel, bool>> ToExpression();

    /// <summary>
    /// Converts this proposition to an explanation proposition (string metadata) while preserving the
    /// underlying predicate expression tree.
    /// </summary>
    /// <returns>An expression-backed explanation proposition.</returns>
    public override SpecBase<TModel, string> ToExplanationSpec() =>
        this as SpecBase<TModel, string>
            ?? (_explanationExpressionSpec ??=
                new ExpressionSpecDecorator<TModel, string>(base.ToExplanationSpec(), ToExpression()));

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

    /// <summary>
    /// Combines this proposition with a same-metadata proposition that is not itself expression-backed,
    /// using the logical AND operator. Because the other operand's predicate cannot be recovered as an
    /// expression, the result degrades to an ordinary (non expression-backed) proposition. Redeclared here
    /// (rather than relying on the inherited base implementation) so this overload remains a candidate of
    /// equal declaring-type precedence to <see cref="And{TSpec}"/>, preserving overload resolution parity.
    /// </summary>
    /// <param name="spec">The proposition to combine with this proposition.</param>
    /// <returns>An ordinary proposition representing the logical AND of the two propositions.</returns>
    public new SpecBase<TModel, TMetadata> And(SpecBase<TModel, TMetadata> spec) =>
        base.And(spec);

    /// <summary>
    /// Combines this proposition with an expression-backed proposition that has a different metadata
    /// type, using the logical AND operator. The operands are coerced to string metadata, and the
    /// result remains expression-backed.
    /// </summary>
    /// <param name="spec">The expression-backed proposition to combine with this proposition.</param>
    /// <typeparam name="TSpec">The type of the other proposition.</typeparam>
    /// <returns>An expression-backed explanation proposition representing the logical AND.</returns>
    public ExpressionSpecBase<TModel, string> And<TSpec>(TSpec spec)
        where TSpec : SpecBase<TModel>, IExpressionSpec<TModel> =>
        new ExpressionAndSpec<TModel, string>(ToExplanationSpec(), spec.ToExplanationSpec(), this, spec);

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
    /// Combines this proposition with a same-metadata proposition that is not itself expression-backed,
    /// using the conditional AND operator. Because the other operand's predicate cannot be recovered as an
    /// expression, the result degrades to an ordinary (non expression-backed) proposition. Redeclared here
    /// (rather than relying on the inherited base implementation) so this overload remains a candidate of
    /// equal declaring-type precedence to <see cref="AndAlso{TSpec}"/>, preserving overload resolution parity.
    /// </summary>
    /// <param name="spec">The proposition to combine with this proposition.</param>
    /// <returns>An ordinary proposition representing the conditional AND of the two propositions.</returns>
    public new SpecBase<TModel, TMetadata> AndAlso(SpecBase<TModel, TMetadata> spec) =>
        base.AndAlso(spec);

    /// <summary>
    /// Combines this proposition with an expression-backed proposition that has a different metadata
    /// type, using the conditional AND operator. The right operand is only evaluated if the left operand
    /// resolves to <c>true</c>. The operands are coerced to string metadata, and the result remains
    /// expression-backed.
    /// </summary>
    /// <param name="spec">The expression-backed proposition to combine with this proposition.</param>
    /// <typeparam name="TSpec">The type of the other proposition.</typeparam>
    /// <returns>An expression-backed explanation proposition representing the conditional AND.</returns>
    public ExpressionSpecBase<TModel, string> AndAlso<TSpec>(TSpec spec)
        where TSpec : SpecBase<TModel>, IExpressionSpec<TModel> =>
        new ExpressionAndAlsoSpec<TModel, string>(ToExplanationSpec(), spec.ToExplanationSpec(), this, spec);

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
    /// Combines this proposition with a same-metadata proposition that is not itself expression-backed,
    /// using the logical OR operator. Because the other operand's predicate cannot be recovered as an
    /// expression, the result degrades to an ordinary (non expression-backed) proposition. Redeclared here
    /// (rather than relying on the inherited base implementation) so this overload remains a candidate of
    /// equal declaring-type precedence to <see cref="Or{TSpec}"/>, preserving overload resolution parity.
    /// </summary>
    /// <param name="spec">The proposition to combine with this proposition.</param>
    /// <returns>An ordinary proposition representing the logical OR of the two propositions.</returns>
    public new SpecBase<TModel, TMetadata> Or(SpecBase<TModel, TMetadata> spec) =>
        base.Or(spec);

    /// <summary>
    /// Combines this proposition with an expression-backed proposition that has a different metadata
    /// type, using the logical OR operator. The operands are coerced to string metadata, and the
    /// result remains expression-backed.
    /// </summary>
    /// <param name="spec">The expression-backed proposition to combine with this proposition.</param>
    /// <typeparam name="TSpec">The type of the other proposition.</typeparam>
    /// <returns>An expression-backed explanation proposition representing the logical OR.</returns>
    public ExpressionSpecBase<TModel, string> Or<TSpec>(TSpec spec)
        where TSpec : SpecBase<TModel>, IExpressionSpec<TModel> =>
        new ExpressionOrSpec<TModel, string>(ToExplanationSpec(), spec.ToExplanationSpec(), this, spec);

    /// <summary>Combines two expression-backed propositions using the logical OR operator.</summary>
    /// <param name="left">The left operand of the OR operation.</param>
    /// <param name="right">The right operand of the OR operation.</param>
    /// <returns>An expression-backed proposition representing the logical OR of the two propositions.</returns>
    public static ExpressionSpecBase<TModel, TMetadata> operator |(
        ExpressionSpecBase<TModel, TMetadata> left,
        ExpressionSpecBase<TModel, TMetadata> right) =>
        left.Or(right);

    /// <inheritdoc cref="op_BitwiseOr(ExpressionSpecBase{TModel, TMetadata}, ExpressionSpecBase{TModel, TMetadata})"/>
    public static ExpressionSpecBase<TModel, TMetadata> operator |(
        ExpressionSpecBase<TModel, TMetadata> left,
        ExpressionPolicyBase<TModel, TMetadata> right) =>
        left.Or(right);

    /// <inheritdoc cref="op_BitwiseOr(ExpressionSpecBase{TModel, TMetadata}, ExpressionSpecBase{TModel, TMetadata})"/>
    public static ExpressionSpecBase<TModel, TMetadata> operator |(
        ExpressionPolicyBase<TModel, TMetadata> left,
        ExpressionSpecBase<TModel, TMetadata> right) =>
        new ExpressionOrSpec<TModel, TMetadata>(left, right, left, right);

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

    /// <inheritdoc cref="OrElse(ExpressionSpecBase{TModel, TMetadata})"/>
    public ExpressionSpecBase<TModel, TMetadata> OrElse(ExpressionPolicyBase<TModel, TMetadata> spec) =>
        new ExpressionOrElseSpec<TModel, TMetadata>(this, spec, this, spec);

    /// <summary>
    /// Combines this proposition with a same-metadata proposition that is not itself expression-backed,
    /// using the conditional OR operator. Because the other operand's predicate cannot be recovered as an
    /// expression, the result degrades to an ordinary (non expression-backed) proposition. Redeclared here
    /// (rather than relying on the inherited base implementation) so this overload remains a candidate of
    /// equal declaring-type precedence to <see cref="OrElse{TSpec}"/>, preserving overload resolution parity.
    /// </summary>
    /// <param name="spec">The proposition to combine with this proposition.</param>
    /// <returns>An ordinary proposition representing the conditional OR of the two propositions.</returns>
    public new SpecBase<TModel, TMetadata> OrElse(SpecBase<TModel, TMetadata> spec) =>
        base.OrElse(spec);

    /// <summary>
    /// Combines this proposition with an expression-backed proposition that has a different metadata
    /// type, using the conditional OR operator. The right operand is only evaluated if the left operand
    /// resolves to <c>false</c>. The operands are coerced to string metadata, and the result remains
    /// expression-backed.
    /// </summary>
    /// <param name="spec">The expression-backed proposition to combine with this proposition.</param>
    /// <typeparam name="TSpec">The type of the other proposition.</typeparam>
    /// <returns>An expression-backed explanation proposition representing the conditional OR.</returns>
    public ExpressionSpecBase<TModel, string> OrElse<TSpec>(TSpec spec)
        where TSpec : SpecBase<TModel>, IExpressionSpec<TModel> =>
        new ExpressionOrElseSpec<TModel, string>(ToExplanationSpec(), spec.ToExplanationSpec(), this, spec);

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

    /// <summary>
    /// Combines this proposition with a same-metadata proposition that is not itself expression-backed,
    /// using the logical XOR operator. Because the other operand's predicate cannot be recovered as an
    /// expression, the result degrades to an ordinary (non expression-backed) proposition. Redeclared here
    /// (rather than relying on the inherited base implementation) so this overload remains a candidate of
    /// equal declaring-type precedence to <see cref="XOr{TSpec}"/>, preserving overload resolution parity.
    /// </summary>
    /// <param name="spec">The proposition to combine with this proposition.</param>
    /// <returns>An ordinary proposition representing the logical XOR of the two propositions.</returns>
    public new SpecBase<TModel, TMetadata> XOr(SpecBase<TModel, TMetadata> spec) =>
        base.XOr(spec);

    /// <summary>
    /// Combines this proposition with an expression-backed proposition that has a different metadata
    /// type, using the logical XOR operator. Both operands are always evaluated. The operands are
    /// coerced to string metadata, and the result remains expression-backed.
    /// </summary>
    /// <param name="spec">The expression-backed proposition to combine with this proposition.</param>
    /// <typeparam name="TSpec">The type of the other proposition.</typeparam>
    /// <returns>An expression-backed explanation proposition representing the logical XOR.</returns>
    public ExpressionSpecBase<TModel, string> XOr<TSpec>(TSpec spec)
        where TSpec : SpecBase<TModel>, IExpressionSpec<TModel> =>
        new ExpressionXOrSpec<TModel, string>(ToExplanationSpec(), spec.ToExplanationSpec(), this, spec);

    /// <summary>Combines two expression-backed propositions using the logical XOR operator.</summary>
    /// <param name="left">The left operand of the XOR operation.</param>
    /// <param name="right">The right operand of the XOR operation.</param>
    /// <returns>An expression-backed proposition representing the logical XOR of the two propositions.</returns>
    public static ExpressionSpecBase<TModel, TMetadata> operator ^(
        ExpressionSpecBase<TModel, TMetadata> left,
        ExpressionSpecBase<TModel, TMetadata> right) =>
        left.XOr(right);

    /// <inheritdoc cref="op_ExclusiveOr(ExpressionSpecBase{TModel, TMetadata}, ExpressionSpecBase{TModel, TMetadata})"/>
    public static ExpressionSpecBase<TModel, TMetadata> operator ^(
        ExpressionSpecBase<TModel, TMetadata> left,
        ExpressionPolicyBase<TModel, TMetadata> right) =>
        left.XOr(right);

    /// <inheritdoc cref="op_ExclusiveOr(ExpressionSpecBase{TModel, TMetadata}, ExpressionSpecBase{TModel, TMetadata})"/>
    public static ExpressionSpecBase<TModel, TMetadata> operator ^(
        ExpressionPolicyBase<TModel, TMetadata> left,
        ExpressionSpecBase<TModel, TMetadata> right) =>
        new ExpressionXOrSpec<TModel, TMetadata>(left, right, left, right);

    /// <summary>Negates this proposition. The result is itself expression-backed.</summary>
    /// <returns>An expression-backed proposition representing the logical NOT of this proposition.</returns>
    public new ExpressionSpecBase<TModel, TMetadata> Not() =>
        new ExpressionNotSpec<TModel, TMetadata>(this, this);

    /// <summary>Negates an expression-backed proposition.</summary>
    /// <param name="spec">The proposition to negate.</param>
    /// <returns>An expression-backed proposition representing the logical NOT of the proposition.</returns>
    public static ExpressionSpecBase<TModel, TMetadata> operator !(
        ExpressionSpecBase<TModel, TMetadata> spec) =>
        spec.Not();
}
