namespace Karlssberg.Motiv.CompositeFactory;

internal sealed class CompositeFactoryMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> underlyingSpecFactory,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenFalse,
    string propositionalAssertion)
    : SpecBase<TModel, TMetadata>
{
    /// <inheritdoc />
    public override IProposition Proposition => new Proposition(propositionalAssertion);

    
    /// <inheritdoc />
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var booleanResult = underlyingSpecFactory(model).IsSatisfiedBy(model);
        
        var metadata = booleanResult.Satisfied switch
        {
            true => whenTrue(model, booleanResult),
            false => whenFalse(model, booleanResult),
        };
        
        var because = metadata switch {
            string reason => reason,
            _ => Proposition.ToReason(booleanResult.Satisfied)
        };

        return new CompositeFactoryBooleanResult<TMetadata, TUnderlyingMetadata>(booleanResult, metadata, because);
    }
}