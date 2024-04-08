namespace Karlssberg.Motiv.CompositeFactory;

internal sealed class CompositeFactoryMultiMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> underlyingSpecFactory,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse,
    string propositionalAssertion)
    : SpecBase<TModel, TMetadata>
{
    public override IProposition Proposition => new Proposition(propositionalAssertion);

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var booleanResult = underlyingSpecFactory(model).IsSatisfiedBy(model);
        
        var metadata = booleanResult.Satisfied switch
        {
            true => whenTrue(model, booleanResult),
            false => whenFalse(model, booleanResult),
        };
        
        var assertions = metadata switch {
            IEnumerable<string> because => because.ToArray(),
            _ => [Proposition.ToReason(booleanResult.Satisfied)]
        };
        
        var explanation = new Explanation(assertions, assertions)
        {
            Underlying = booleanResult.Explanation.ToEnumerable()
        };

        return new CompositeFactoryBooleanResult<TMetadata, TUnderlyingMetadata>(
            booleanResult,
            metadata,
            explanation,
            Proposition.ToReason(booleanResult.Satisfied));
    }
}