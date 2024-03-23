namespace Karlssberg.Motiv.Composite;

internal sealed class CompositeMultiMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse,
    string propositionalAssertion)
    : SpecBase<TModel, TMetadata>
{
    /// <inheritdoc />
    public override IProposition Proposition => new Proposition(propositionalAssertion, underlyingSpec.Proposition);

    /// <inheritdoc />
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var booleanResult = underlyingSpec.IsSatisfiedBy(model);
        
        var metadata = booleanResult.Satisfied switch
        {
            true => whenTrue(model, booleanResult),
            false => whenFalse(model, booleanResult),
        };
        
        var metadataSet = new MetadataTree<TMetadata>(metadata);

        return new CompositeMultiMetadataBooleanResult<TMetadata, TUnderlyingMetadata>(booleanResult, metadataSet, Proposition);
    }
}