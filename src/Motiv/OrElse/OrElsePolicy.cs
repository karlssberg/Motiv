using Motiv.Or;
using Motiv.Shared;
using Motiv.Traversal;

namespace Motiv.OrElse;

internal sealed class OrElsePolicy<TModel, TMetadata>(
    PolicyBase<TModel, TMetadata> left,
    PolicyBase<TModel, TMetadata> right)
    : PolicyBase<TModel, TMetadata>,
        IBinaryOperationSpec<TModel, TMetadata>,
        IOperationFold<TModel, TMetadata>,
        IBinaryOperationSpec<TModel>,
        IBinaryOperationSpec
{
    private readonly SpecBase[] _underlying = [left, right];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => field ??=
        new BinarySpecDescription<TModel, TMetadata>(left, right, "||", Operator.OrElse,
            operand => operand is OrSpec<TModel, TMetadata> or OrElsePolicy<TModel, TMetadata>
                or OrElseSpec<TModel, TMetadata> or ExpressionOrSpec<TModel, TMetadata>
                or ExpressionOrElseSpec<TModel, TMetadata> or ExpressionOrElsePolicy<TModel, TMetadata>);

    public string Operation => Operator.OrElse;

    public bool IsCollapsable => true;

    public override bool Matches(TModel model) => EvaluationFold.Matches(this, model);

    protected override PolicyResultBase<TMetadata> EvaluatePolicy(TModel model) =>
        EvaluationFold.EvaluatePolicy(this, model);

    SpecBase<TModel, TMetadata> IOperationFold<TModel, TMetadata>.FirstOperand => left;

    SpecBase<TModel, TMetadata>? IOperationFold<TModel, TMetadata>.NextOperand(bool firstSatisfied) =>
        firstSatisfied ? null : right;

    BooleanResultBase<TMetadata> IOperationFold<TModel, TMetadata>.Combine(
        BooleanResultBase<TMetadata> first,
        BooleanResultBase<TMetadata>? second) =>
        new OrElsePolicyResult<TMetadata>((PolicyResultBase<TMetadata>)first, (PolicyResultBase<TMetadata>?)second);

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
