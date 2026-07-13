using System.Linq.Expressions;

namespace Motiv.ExpressionTreeProposition;

internal sealed class ExpressionSpecDecorator<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> underlyingSpec,
    Expression<Func<TModel, bool>> expression)
    : ExpressionSpecBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => underlyingSpec.Underlying;

    public override ISpecDescription Description => underlyingSpec.Description;

    public override Expression<Func<TModel, bool>> ToExpression() => expression;

    public override bool Matches(TModel model) => underlyingSpec.Matches(model);

    protected override BooleanResultBase<TMetadata> EvaluateSpec(TModel model) =>
        underlyingSpec.EvaluateInternal(model);
}
