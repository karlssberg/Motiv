using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;
using Motiv.Traversal;

namespace Motiv.Not;

internal sealed class ExpressionNotPolicy<TModel, TMetadata>(
    ExpressionPolicyBase<TModel, TMetadata> operand)
    : ExpressionPolicyBase<TModel, TMetadata>,
        IUnaryOperationSpec<TModel, TMetadata>,
        IOperationFold<TModel, TMetadata>,
        IUnaryOperationSpec<TModel>,
        IUnaryOperationSpec
{
    private readonly SpecBase[] _underlying = [operand];

    private readonly Lazy<Expression<Func<TModel, bool>>> _expression = new(() =>
        ExpressionComposer.Negate(operand));

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => field ??=
        new NotSpecDescription<TModel, TMetadata>(operand);

    string IBooleanOperationSpec.Operation => Operator.Not;

    bool IBooleanOperationSpec.IsCollapsable => false;

    public override Expression<Func<TModel, bool>> ToExpression() => _expression.Value;

    public override bool Matches(TModel model) => EvaluationFold.Matches(this, model);

    protected override PolicyResultBase<TMetadata> EvaluatePolicy(TModel model) =>
        EvaluationFold.EvaluatePolicy(this, model);

    SpecBase<TModel, TMetadata> IOperationFold<TModel, TMetadata>.FirstOperand => operand;

    SpecBase<TModel, TMetadata>? IOperationFold<TModel, TMetadata>.NextOperand(bool firstSatisfied) => null;

    BooleanResultBase<TMetadata> IOperationFold<TModel, TMetadata>.Combine(
        BooleanResultBase<TMetadata> first,
        BooleanResultBase<TMetadata>? second) =>
        ((PolicyResultBase<TMetadata>)first).Not();

    bool IOperationFold<TModel, TMetadata>.CombineMatches(bool first, bool? second) => !first;

    public PolicyBase<TModel, TMetadata> Operand => operand;

    SpecBase<TModel, TMetadata> IUnaryOperationSpec<TModel, TMetadata>.Operand => operand;

    SpecBase<TModel> IUnaryOperationSpec<TModel>.Operand => operand;

    SpecBase IUnaryOperationSpec.Operand => operand;
}
