namespace Motiv.AndAlso;

internal sealed class AndAlsoSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right)
    : SpecBase<TModel, TMetadata>, IBinaryOperationSpec<TModel, TMetadata>
{
    public override ISpecDescription Description => 
        new AndAlsoSpecDescription<TModel, TMetadata>(left, right);

    public string Operation => "AND ALSO";
    public bool IsCollapsable => true;

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var leftResult = left.IsSatisfiedBy(model);
        return leftResult.Satisfied switch
        {
            true =>  new AndAlsoBooleanResult<TMetadata>(
                leftResult,
                right.IsSatisfiedBy(model)),
            false => new AndAlsoBooleanResult<TMetadata>(leftResult)
        };
    }

    public SpecBase<TModel, TMetadata> Left => left;
    public SpecBase<TModel, TMetadata> Right => right;
}