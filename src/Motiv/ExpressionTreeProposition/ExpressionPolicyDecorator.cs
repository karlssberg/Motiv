using System.Linq.Expressions;

namespace Motiv.ExpressionTreeProposition;

internal sealed class ExpressionPolicyDecorator<TModel, TMetadata>(
    PolicyBase<TModel, TMetadata> underlyingPolicy,
    Expression<Func<TModel, bool>> expression)
    : ExpressionPolicyBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => underlyingPolicy.Underlying;

    public override ISpecDescription Description => underlyingPolicy.Description;

    public override Expression<Func<TModel, bool>> ToExpression() => expression;

    public override bool Matches(TModel model) => underlyingPolicy.Matches(model);

    protected override PolicyResultBase<TMetadata> EvaluatePolicy(TModel model) =>
        underlyingPolicy.EvaluatePolicyInternal(model);
}
