namespace Karlssberg.Motiv.OrElse;

internal sealed class OrElseSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right)
    : SpecBase<TModel, TMetadata>, ICompositeSpec
{
    public override IProposition Proposition => 
        new OrElseProposition<TModel, TMetadata>(left, right);

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var antecedentResult = left.IsSatisfiedByWithExceptionRethrowing(model);
        return antecedentResult.Satisfied switch
        {
            true => new OrElseBooleanResult<TMetadata>(antecedentResult),
            false => new OrElseBooleanResult<TMetadata>(
                antecedentResult,
                right.IsSatisfiedByWithExceptionRethrowing(model))
        };
    }
}