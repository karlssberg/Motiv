namespace Motiv.XOr;

internal sealed class XOrSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right)
    : SpecBase<TModel, TMetadata>, IBinaryOperationSpec<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => left.ToEnumerable().Append(right);
    
    public override ISpecDescription Description => 
        new XOrSpecDescription<TModel, TMetadata>(left, right);

    public string Operation => "XOR";
    public bool IsCollapsable => false;

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var leftResult = left.IsSatisfiedBy(model);
        var rightResult = right.IsSatisfiedBy(model);

        return leftResult.XOr(rightResult);
    }

    public SpecBase<TModel, TMetadata> Left => left;
    public SpecBase<TModel, TMetadata> Right => right;
}