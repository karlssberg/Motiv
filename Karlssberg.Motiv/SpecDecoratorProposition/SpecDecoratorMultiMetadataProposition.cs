namespace Karlssberg.Motiv.SpecDecoratorProposition;

internal sealed class SpecDecoratorMultiMetadataProposition<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse,
    ISpecDescription description)
    : SpecBase<TModel, TMetadata>
{
    public override ISpecDescription Description => description;

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var booleanResult = underlyingSpec.IsSatisfiedBy(model);
        
        var metadata = new Lazy<IEnumerable<TMetadata>>(() => booleanResult.Satisfied switch
        {
            true => whenTrue(model, booleanResult),
            false => whenFalse(model, booleanResult)
        });
        
        var assertions = new Lazy<string[]>(() => metadata.Value switch {
            IEnumerable<string> because => because.ToArray(),
            _ => [Description.ToReason(booleanResult.Satisfied)]
        });

        return new BooleanResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
            booleanResult,
            MetadataTree,
            Explanation,
            () => Description.ToReason(booleanResult.Satisfied));

        Explanation Explanation() => new(assertions.Value)
        {
            Underlying = booleanResult.Explanation.ToEnumerable()
        };

        MetadataTree<TMetadata> MetadataTree() => 
            new(metadata.Value, 
                booleanResult.ResolveMetadataTrees<TMetadata, TUnderlyingMetadata>());
    }
}