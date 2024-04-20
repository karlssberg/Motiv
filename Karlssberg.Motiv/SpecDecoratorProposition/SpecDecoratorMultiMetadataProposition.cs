﻿namespace Karlssberg.Motiv.SpecDecoratorProposition;

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
        
        var metadata = new Lazy<IEnumerable<TMetadata>>(() =>
            booleanResult.Satisfied switch
            {
                true => whenTrue(model, booleanResult),
                false => whenFalse(model, booleanResult)
            });
        
        var assertions = new Lazy<string[]>(() =>
            metadata.Value switch
            {
                IEnumerable<string> because => because.ToArray(),
                _ => [Description.ToReason(booleanResult.Satisfied)]
            });

        var explanation = new Lazy<Explanation>(() => 
            new Explanation(assertions.Value, booleanResult.ToEnumerable()));
        
        var metadataTree = new Lazy<MetadataNode<TMetadata>>(() => 
            new MetadataNode<TMetadata>(metadata.Value, 
                booleanResult.MetadataTier.ToEnumerable() as IEnumerable<MetadataNode<TMetadata>> ?? []));

        return new BooleanResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
            booleanResult,
            MetadataTree,
            Explanation,
            Reason);

        string Reason() => Description.ToReason(booleanResult.Satisfied);
        Explanation Explanation() => explanation.Value;
        MetadataNode<TMetadata> MetadataTree() => metadataTree.Value;
    }
}