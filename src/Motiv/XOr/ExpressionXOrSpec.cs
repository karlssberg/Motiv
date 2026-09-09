using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;
using Motiv.Shared;
using Motiv.Traversal;
using Expr = System.Linq.Expressions.Expression;

namespace Motiv.XOr;

internal sealed class ExpressionXOrSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right,
    IExpressionSpec<TModel> leftExpression,
    IExpressionSpec<TModel> rightExpression)
    : ExpressionSpecBase<TModel, TMetadata>,
        IBinaryOperationSpec<TModel, TMetadata>,
        IFoldableOperation<TModel, TMetadata>,
        IBinaryOperationSpec<TModel>,
        IBinaryOperationSpec
{
    private readonly SpecBase[] _underlying = [left, right];

    private readonly Lazy<Expression<Func<TModel, bool>>> _expression = new(() =>
        ExpressionComposer.Combine(leftExpression, rightExpression, Expr.ExclusiveOr));

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => field ??=
        new BinarySpecDescription<TModel, TMetadata>(left, right, "^", Operator.XOr,
            operand => operand is XOrSpec<TModel, TMetadata> or ExpressionXOrSpec<TModel, TMetadata>);

    public string Operation => Operator.XOr;

    public bool IsCollapsable => false;

    public override Expression<Func<TModel, bool>> ToExpression() => _expression.Value;

    public override bool Matches(TModel model) => EvaluationFold.Matches(this, model);

    protected override BooleanResultBase<TMetadata> EvaluateSpec(TModel model) =>
        EvaluationFold.Evaluate(this, model);

    SpecBase<TModel, TMetadata> IFoldableOperation<TModel, TMetadata>.FirstOperand => left;

    SpecBase<TModel, TMetadata>? IFoldableOperation<TModel, TMetadata>.NextOperand(bool firstSatisfied) => right;

    BooleanResultBase<TMetadata> IFoldableOperation<TModel, TMetadata>.Combine(
        BooleanResultBase<TMetadata> first,
        BooleanResultBase<TMetadata>? second) =>
        first.XOr(second!);

    bool IFoldableOperation<TModel, TMetadata>.CombineMatches(bool first, bool? second) => first ^ second!.Value;

    public SpecBase<TModel, TMetadata> Left => left;

    public SpecBase<TModel, TMetadata> Right => right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Right => Right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Left => Left;

    SpecBase IBinaryOperationSpec.Right => Right;

    SpecBase IBinaryOperationSpec.Left => Left;
}
