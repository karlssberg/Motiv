namespace Karlssberg.Motiv.OrElse;

internal sealed class OrElseSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right)
    : SpecBase<TModel, TMetadata>, IBinaryOperationSpec
{
    public override ISpecDescription Description => 
        new OrElseSpecDescription<TModel, TMetadata>(left, right);

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var antecedentResult = left.IsSatisfiedBy(model);
        return antecedentResult.Satisfied switch
        {
            true => new OrElseBooleanResult<TMetadata>(antecedentResult),
            false => new OrElseBooleanResult<TMetadata>(
                antecedentResult,
                right.IsSatisfiedBy(model))
        };
    }
}