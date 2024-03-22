namespace Karlssberg.Motiv.ElseIf;

internal sealed class ElseIfSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> antecedent,
    SpecBase<TModel, TMetadata> consequent)
    : SpecBase<TModel, TMetadata>, ICompositeSpec
{
    /// <inheritdoc />
    public override IProposition Proposition => 
        new ElseIfProposition<TModel>(antecedent, consequent);

    /// <inheritdoc />
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var antecedentResult = antecedent.IsSatisfiedByWithExceptionRethrowing(model);
        return antecedentResult.Satisfied switch
        {
            true => antecedentResult,
            false => new ConsequentBooleanResult<TMetadata>(
                antecedentResult,
                consequent.IsSatisfiedByWithExceptionRethrowing(model))
        };
    }
}