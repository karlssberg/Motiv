using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;
using Motiv.Traversal;

namespace Motiv.Not;

internal sealed class ExpressionNotSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> operand,
    IExpressionSpec<TModel> operandExpression)
    : ExpressionSpecBase<TModel, TMetadata>,
        IUnaryOperationSpec<TModel, TMetadata>,
        IUnaryOperationSpec<TModel>,
        IUnaryOperationSpec
{
    private readonly SpecBase[] _underlying = [operand];

    private readonly Lazy<Expression<Func<TModel, bool>>> _expression = new(() =>
        ExpressionComposer.Negate(operandExpression));

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => field ??=
        new NotSpecDescription<TModel, TMetadata>(operand);

    public string Operation => Operator.Not;

    public bool IsCollapsable => false;

    public override Expression<Func<TModel, bool>> ToExpression() => _expression.Value;

    public override bool Matches(TModel model) => !operand.Matches(model);

    protected override BooleanResultBase<TMetadata> EvaluateSpec(TModel model) =>
        operand.Evaluate(model).Not();

    public SpecBase<TModel, TMetadata> Operand => operand;

    SpecBase<TModel> IUnaryOperationSpec<TModel>.Operand => operand;

    SpecBase IUnaryOperationSpec.Operand => operand;
}
