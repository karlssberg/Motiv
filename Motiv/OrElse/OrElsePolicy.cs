namespace Motiv.OrElse;

internal sealed class OrElsePolicy<TModel, TMetadata>(
    PolicyBase<TModel, TMetadata> left,
    PolicyBase<TModel, TMetadata> right)
    : PolicyBase<TModel, TMetadata>,
        IBinaryOperationSpec<TModel, TMetadata>,
        IBinaryOperationSpec<TModel>,
        IBinaryOperationSpec
{

    public override IEnumerable<SpecBase> Underlying => left.ToEnumerable().Append(right);

    public override ISpecDescription Description =>
        new OrElseSpecDescription<TModel, TMetadata>(left, right);

    public string Operation => Operator.OrElse;

    public bool IsCollapsable => true;

    protected override PolicyResultBase<TMetadata> IsPolicySatisfiedBy(TModel model)
    {
        var leftResult = left.IsSatisfiedBy(model);
        return leftResult.Satisfied switch
        {
            true => new OrElsePolicyResult<TMetadata>(leftResult),
            false => new OrElsePolicyResult<TMetadata>(leftResult, right.IsSatisfiedBy(model))
        };
    }

    public SpecBase<TModel, TMetadata> Left => left;

    public SpecBase<TModel, TMetadata> Right => right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Right => Right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Left => Left;

    SpecBase IBinaryOperationSpec.Right => Right;

    SpecBase IBinaryOperationSpec.Left => Left;
}
