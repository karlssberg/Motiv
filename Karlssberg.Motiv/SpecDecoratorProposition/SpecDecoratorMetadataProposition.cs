namespace Karlssberg.Motiv.SpecDecoratorProposition;

internal sealed class SpecDecoratorMetadataProposition<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenFalse,
    ISpecDescription description)
    : SpecBase<TModel, TMetadata>
{
    public override ISpecDescription Description => description;

    public SpecBase<TModel, TUnderlyingMetadata> UnderlyingSpec { get; } = underlyingSpec;

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var booleanResult = UnderlyingSpec.IsSatisfiedBy(model);
        
        var metadata = new Lazy<TMetadata>(() =>
            booleanResult.Satisfied switch
            {
                true => whenTrue(model, booleanResult),
                false => whenFalse(model, booleanResult),
            });
        
        var assertion = new Lazy<string>(() =>
            metadata.Value switch
            {
                string because => because,
                _ => Description.ToReason(booleanResult.Satisfied)
            });

        var explanation = new Lazy<Explanation>(() =>
            new Explanation(assertion.Value)
            {
                Underlying = booleanResult.Explanation.ToEnumerable()
            });
        
        var metadataTree = new Lazy<MetadataTree<TMetadata>>(() => 
            new MetadataTree<TMetadata>(metadata.Value, 
                booleanResult.ResolveMetadataTrees<TMetadata, TUnderlyingMetadata>()));

        return new BooleanResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
            booleanResult,
            MetadataTree,
            Explanation,
            Reason);
        
        MetadataTree<TMetadata> MetadataTree() => metadataTree.Value;
        Explanation Explanation() => explanation.Value;
        string Reason() => Description.ToReason(booleanResult.Satisfied);
    }
}