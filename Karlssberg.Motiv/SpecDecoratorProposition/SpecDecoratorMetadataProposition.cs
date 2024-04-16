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
        
        var metadata = new Lazy<TMetadata>(() => booleanResult.Satisfied switch
        {
            true => whenTrue(model, booleanResult),
            false => whenFalse(model, booleanResult),
        });
        
        var assertion = new Lazy<string>(() => metadata.Value switch {
            string because => because,
            _ => Description.ToReason(booleanResult.Satisfied)
        });

        return new BooleanResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
            booleanResult,
            MetadataTree,
            Explanation,
            () => Description.ToReason(booleanResult.Satisfied));

        Explanation Explanation() => new(assertion.Value)
        {
            Underlying = booleanResult.Explanation.ToEnumerable()
        };

        MetadataTree<TMetadata> MetadataTree() => 
            new(metadata.Value, 
                booleanResult.ResolveMetadataTrees<TMetadata, TUnderlyingMetadata>());
    }
}