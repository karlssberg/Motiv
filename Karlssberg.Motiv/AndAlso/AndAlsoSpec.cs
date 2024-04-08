namespace Karlssberg.Motiv.AndAlso;

internal sealed class AndAlsoSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right)
    : SpecBase<TModel, TMetadata>, IBinaryOperationSpec
{
    public override IProposition Proposition => 
        new AndAlsoProposition<TModel, TMetadata>(left, right);

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var antecedentResult = left.IsSatisfiedByWithExceptionRethrowing(model);
        return antecedentResult.Satisfied switch
        {
            true =>  new AndAlsoBooleanResult<TMetadata>(
                antecedentResult,
                right.IsSatisfiedByWithExceptionRethrowing(model)),
            false => new AndAlsoBooleanResult<TMetadata>(antecedentResult)
        };
    }
}