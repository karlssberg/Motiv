using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;
using Motiv.OrElse;
using Motiv.Shared;
using Motiv.Traversal;
using Expr = System.Linq.Expressions.Expression;

namespace Motiv.Or;

internal sealed class ExpressionOrSpec<TModel, TMetadata>(
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
        ExpressionComposer.Combine(leftExpression, rightExpression, Expr.Or));

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => field ??=
        new BinarySpecDescription<TModel, TMetadata>(left, right, "|", Operator.Or,
            operand => operand is OrSpec<TModel, TMetadata> or OrElseSpec<TModel, TMetadata>
                or OrElsePolicy<TModel, TMetadata> or ExpressionOrSpec<TModel, TMetadata>
                or ExpressionOrElseSpec<TModel, TMetadata> or ExpressionOrElsePolicy<TModel, TMetadata>);

    public string Operation => Operator.Or;

    public bool IsCollapsable => true;

    public SpecBase<TModel, TMetadata> Left => left;

    public SpecBase<TModel, TMetadata> Right => right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Right => Right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Left => Left;

    SpecBase IBinaryOperationSpec.Right => Right;

    SpecBase IBinaryOperationSpec.Left => Left;

    public override Expression<Func<TModel, bool>> ToExpression() => _expression.Value;

    public override bool Matches(TModel model) => EvaluationFold.Matches(this, model);

    protected override BooleanResultBase<TMetadata> EvaluateSpec(TModel model) =>
        EvaluationFold.Evaluate(this, model);

    SpecBase<TModel, TMetadata> IFoldableOperation<TModel, TMetadata>.FirstOperand => left;

    SpecBase<TModel, TMetadata>? IFoldableOperation<TModel, TMetadata>.NextOperand(bool firstSatisfied) => right;

    BooleanResultBase<TMetadata> IFoldableOperation<TModel, TMetadata>.Combine(
        BooleanResultBase<TMetadata> first,
        BooleanResultBase<TMetadata>? second) =>
        first.Or(second!);

    bool IFoldableOperation<TModel, TMetadata>.CombineMatches(bool first, bool? second) => first | second!.Value;
}
