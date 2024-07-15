namespace Motiv.OrElse;

internal sealed class OrElsePolicy<TModel, TMetadata>(
    PolicyBase<TModel, TMetadata> left,
    PolicyBase<TModel, TMetadata> right)
    : PolicyBase<TModel, TMetadata>, IBinaryOperationSpec<TModel, TMetadata>
{

    public override IEnumerable<SpecBase> Underlying => left.ToEnumerable().Append(right);

    public override ISpecDescription Description =>
        new OrElseSpecDescription<TModel, TMetadata>(left, right);

    public string Operation => "OR ELSE";
    public bool IsCollapsable => true;

    public override PolicyResultBase<TMetadata> IsSatisfiedBy(TModel model)
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
}
