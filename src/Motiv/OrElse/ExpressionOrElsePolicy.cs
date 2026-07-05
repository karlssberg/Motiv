using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;
using Motiv.Or;
using Motiv.Shared;
using Motiv.Traversal;
using Expr = System.Linq.Expressions.Expression;

namespace Motiv.OrElse;

internal sealed class ExpressionOrElsePolicy<TModel, TMetadata>(
    ExpressionPolicyBase<TModel, TMetadata> left,
    ExpressionPolicyBase<TModel, TMetadata> right)
    : ExpressionPolicyBase<TModel, TMetadata>,
        IBinaryOperationSpec<TModel, TMetadata>,
        IBinaryOperationSpec<TModel>,
        IBinaryOperationSpec
{
    private readonly SpecBase[] _underlying = [left, right];

    private readonly Lazy<Expression<Func<TModel, bool>>> _expression = new(() =>
        ExpressionComposer.Combine(left, right, Expr.OrElse));

    public override IEnumerable<SpecBase> Underlying => _underlying;

    private ISpecDescription? _description;

    public override ISpecDescription Description => _description ??=
        new BinarySpecDescription<TModel, TMetadata>(left, right, "||", Operator.OrElse,
            operand => operand is OrSpec<TModel, TMetadata> or OrElsePolicy<TModel, TMetadata>
                or OrElseSpec<TModel, TMetadata> or ExpressionOrSpec<TModel, TMetadata>
                or ExpressionOrElseSpec<TModel, TMetadata> or ExpressionOrElsePolicy<TModel, TMetadata>);

    public string Operation => Operator.OrElse;

    public bool IsCollapsable => true;

    public override Expression<Func<TModel, bool>> ToExpression() => _expression.Value;

    public override bool Matches(TModel model) => left.Matches(model) || right.Matches(model);

    protected override PolicyResultBase<TMetadata> EvaluatePolicy(TModel model)
    {
        var leftResult = left.Evaluate(model);
        return leftResult.Satisfied switch
        {
            true => new OrElsePolicyResult<TMetadata>(leftResult),
            false => new OrElsePolicyResult<TMetadata>(leftResult, right.Evaluate(model))
        };
    }

    public PolicyBase<TModel, TMetadata> Left => left;

    public PolicyBase<TModel, TMetadata> Right => right;

    SpecBase<TModel, TMetadata> IBinaryOperationSpec<TModel, TMetadata>.Left => left;

    SpecBase<TModel, TMetadata> IBinaryOperationSpec<TModel, TMetadata>.Right => right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Right => Right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Left => Left;

    SpecBase IBinaryOperationSpec.Right => Right;

    SpecBase IBinaryOperationSpec.Left => Left;
}
