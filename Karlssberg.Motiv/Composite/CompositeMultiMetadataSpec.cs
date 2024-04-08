namespace Karlssberg.Motiv.Composite;

internal sealed class CompositeMultiMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse,
    string propositionalAssertion)
    : SpecBase<TModel, TMetadata>
{
    public override IProposition Proposition => new Proposition(propositionalAssertion, underlyingSpec.Proposition);

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var booleanResult = underlyingSpec.IsSatisfiedBy(model);
        
        var metadata = booleanResult.Satisfied switch
        {
            true => whenTrue(model, booleanResult),
            false => whenFalse(model, booleanResult)
        };
        
        var assertions = metadata switch {
            IEnumerable<string> because => because.ToArray(),
            _ => [Proposition.ToReason(booleanResult.Satisfied)]
        };
        
        var metadataTree = new MetadataTree<TMetadata>(
            metadata,
            booleanResult.ResolveMetadataTrees<TMetadata, TUnderlyingMetadata>());
        
        var explanation = new Explanation(assertions, assertions)
        {
            Underlying = booleanResult.Explanation.ToEnumerable()
        };

        return new BooleanResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
            booleanResult,
            metadataTree,
            explanation,
            Proposition.ToReason(booleanResult.Satisfied));
    }
}