using System.Linq.Expressions;
using Motiv.And;
using Motiv.ExpressionTreeProposition;
using Motiv.Shared;
using Motiv.Traversal;
using Expr = System.Linq.Expressions.Expression;

namespace Motiv.AndAlso;

internal sealed class ExpressionAndAlsoPolicy<TModel, TMetadata>(
    ExpressionPolicyBase<TModel, TMetadata> left,
    ExpressionPolicyBase<TModel, TMetadata> right)
    : ExpressionPolicyBase<TModel, TMetadata>,
        IBinaryOperationSpec<TModel, TMetadata>,
        IOperationFold<TModel, TMetadata>,
        IBinaryOperationSpec<TModel>,
        IBinaryOperationSpec
{
    private readonly SpecBase[] _underlying = [left, right];

    private readonly Lazy<Expression<Func<TModel, bool>>> _expression = new(() =>
        ExpressionComposer.Combine(left, right, Expr.AndAlso));

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => field ??=
        new BinarySpecDescription<TModel, TMetadata>(left, right, "&&", Operator.AndAlso,
            operand => operand is AndSpec<TModel, TMetadata> or AndAlsoPolicy<TModel, TMetadata>
                or AndAlsoSpec<TModel, TMetadata> or ExpressionAndSpec<TModel, TMetadata>
                or ExpressionAndAlsoSpec<TModel, TMetadata> or ExpressionAndAlsoPolicy<TModel, TMetadata>);

    public string Operation => Operator.AndAlso;

    public bool IsCollapsable => true;

    public override Expression<Func<TModel, bool>> ToExpression() => _expression.Value;

    public override bool Matches(TModel model) => EvaluationFold.Matches(this, model);

    protected override PolicyResultBase<TMetadata> EvaluatePolicy(TModel model) =>
        EvaluationFold.EvaluatePolicy(this, model);

    SpecBase<TModel, TMetadata> IOperationFold<TModel, TMetadata>.FirstOperand => left;

    SpecBase<TModel, TMetadata>? IOperationFold<TModel, TMetadata>.NextOperand(bool firstSatisfied) =>
        firstSatisfied ? right : null;

    BooleanResultBase<TMetadata> IOperationFold<TModel, TMetadata>.Combine(
        BooleanResultBase<TMetadata> first,
        BooleanResultBase<TMetadata>? second) =>
        new AndAlsoPolicyResult<TMetadata>((PolicyResultBase<TMetadata>)first, (PolicyResultBase<TMetadata>?)second);

    bool IOperationFold<TModel, TMetadata>.CombineMatches(bool first, bool? second) => second ?? first;

    public PolicyBase<TModel, TMetadata> Left => left;

    public PolicyBase<TModel, TMetadata> Right => right;

    SpecBase<TModel, TMetadata> IBinaryOperationSpec<TModel, TMetadata>.Left => left;

    SpecBase<TModel, TMetadata> IBinaryOperationSpec<TModel, TMetadata>.Right => right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Right => Right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Left => Left;

    SpecBase IBinaryOperationSpec.Right => Right;

    SpecBase IBinaryOperationSpec.Left => Left;
}
