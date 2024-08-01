namespace Motiv.OrElse;

internal sealed class OrElseSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right)
    : SpecBase<TModel, TMetadata>,
        IBinaryOperationSpec<TModel, TMetadata>,
        IBinaryOperationSpec<TModel>,
        IBinaryOperationSpec
{

    public override IEnumerable<SpecBase> Underlying => left.ToEnumerable().Append(right);

    public override ISpecDescription Description =>
        new OrElseSpecDescription<TModel, TMetadata>(left, right);

    public string Operation => Operator.OrElse;

    public bool IsCollapsable => true;

    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(TModel model)
    {
        var leftResult = left.IsSatisfiedBy(model);
        return leftResult.Satisfied switch
        {
            true => new OrElseBooleanResult<TMetadata>(leftResult),
            false => new OrElseBooleanResult<TMetadata>(
                leftResult,
                right.IsSatisfiedBy(model))
        };
    }

    public SpecBase<TModel, TMetadata> Left => left;
    SpecBase<TModel> IBinaryOperationSpec<TModel>.Right => Right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Left => Left;

    public SpecBase<TModel, TMetadata> Right => right;

    SpecBase IBinaryOperationSpec.Right => Right;

    SpecBase IBinaryOperationSpec.Left => Left;
}
