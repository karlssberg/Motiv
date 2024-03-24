namespace Karlssberg.Motiv.CompositeFactory;

internal sealed class CompositeFactoryMultiMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> underlyingSpecFactory,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse,
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
        
        var assertions = metadata switch {
            IEnumerable<string> reasons => reasons,
            _ => Proposition.ToReason(booleanResult.Satisfied).ToEnumerable()
        };

        return new CompositeFactoryBooleanResult<TMetadata, TUnderlyingMetadata>(
            booleanResult,
            metadata,
            assertions,
            Proposition.ToReason(booleanResult.Satisfied));
    }
}