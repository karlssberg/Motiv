namespace Motiv.And;

internal sealed class AndSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right)
    : SpecBase<TModel, TMetadata>, IBinaryOperationSpec<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => left.ToEnumerable().Append(right);

    public override ISpecDescription Description =>
        new AndSpecDescription<TModel, TMetadata>(left, right);

    public string Operation => "AND";
    public bool IsCollapsable => true;

    public SpecBase<TModel, TMetadata> Left => left;
    public SpecBase<TModel, TMetadata> Right => right;

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var leftResult = left.IsSatisfiedBy(model);
        var rightResult = right.IsSatisfiedBy(model);

        return leftResult.And(rightResult);
    }
}