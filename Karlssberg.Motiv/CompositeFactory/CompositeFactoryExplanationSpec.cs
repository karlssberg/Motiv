namespace Karlssberg.Motiv.CompositeFactory;

internal sealed class CompositeFactoryExplanationSpec<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> underlyingSpecFactory,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> falseBecause,
    string propositionalAssertion)
    : SpecBase<TModel, string>
{
    
    /// <inheritdoc />
    public override IProposition Proposition => new Proposition(propositionalAssertion);

    
    /// <inheritdoc />
    public override BooleanResultBase<string> IsSatisfiedBy(TModel model)
    {
        var booleanResult = underlyingSpecFactory(model).IsSatisfiedBy(model);
        
        var assertion = booleanResult.Satisfied switch
        {
            true => trueBecause(model, booleanResult),
            false => falseBecause(model, booleanResult),
        };

        return new CompositeFactoryBooleanResult<string, TUnderlyingMetadata>(
            booleanResult, 
            assertion.ToEnumerable(), 
            assertion.ToEnumerable(),
            Proposition.ToReason(booleanResult.Satisfied));
    }
}