namespace Motiv.OrElse;

internal sealed class OrElseSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right)
    : SpecBase<TModel, TMetadata>, IBinaryOperationSpec<TModel, TMetadata>
{
    
    public override IEnumerable<SpecBase> Underlying => left.ToEnumerable().Append(right);
    
    public override ISpecDescription Description => 
        new OrElseSpecDescription<TModel, TMetadata>(left, right);

    public string Operation => "OR ELSE";
    public bool IsCollapsable => true;

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
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
    public SpecBase<TModel, TMetadata> Right => right;
}