namespace Motiv.SpecDecoratorProposition;

internal sealed class SpecDecoratorMultiMetadataProposition<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse,
    ISpecDescription description)
    : SpecBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => underlyingSpec.ToEnumerable();

    public override ISpecDescription Description => description;

    internal override BooleanResultBase<TMetadata> IsSatisfiedByInternal(TModel model)
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

        var metadataTier = new Lazy<MetadataNode<TMetadata>>(() =>
            new MetadataNode<TMetadata>(metadata.Value,
                booleanResult.ToEnumerable() as IEnumerable<BooleanResultBase<TMetadata>> ?? []));

        return new BooleanResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
            booleanResult,
            MetadataTier,
            Explanation,
            Reason);

        string Reason() =>  Description.ToReason(booleanResult.Satisfied);
        Explanation Explanation() => explanation.Value;
        MetadataNode<TMetadata> MetadataTier() => metadataTier.Value;
    }
}
