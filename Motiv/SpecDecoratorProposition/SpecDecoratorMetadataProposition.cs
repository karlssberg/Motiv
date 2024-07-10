namespace Motiv.SpecDecoratorProposition;

internal sealed class SpecDecoratorMetadataProposition<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenFalse,
    ISpecDescription description)
    : PolicyBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => underlyingSpec.ToEnumerable();


    public override ISpecDescription Description => description;

    public override PolicyResultBase<TMetadata> Execute(TModel model)
    {
        var booleanResult = underlyingSpec.IsSatisfiedBy(model);
        var lazyMetadata = CreateLazyMetadata(model, booleanResult);

        return CreatePolicyResult(lazyMetadata, booleanResult);
    }

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model) =>
        Execute(model);

    private Lazy<TMetadata> CreateLazyMetadata(TModel model, BooleanResultBase<TUnderlyingMetadata> booleanResult)
    {
        return new Lazy<TMetadata>(() =>
            booleanResult.Satisfied switch
            {
                true => whenTrue(model, booleanResult),
                false => whenFalse(model, booleanResult)
            });
    }

    private PolicyResultBase<TMetadata> CreatePolicyResult(Lazy<TMetadata> metadata, BooleanResultBase<TUnderlyingMetadata> booleanResult)
    {
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

        return new PolicyResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
            booleanResult,
            Value,
            MetadataTier,
            Explanation,
            Reason);

        TMetadata Value() => metadata.Value;
        string Reason() =>  Description.ToReason(booleanResult.Satisfied);
        Explanation Explanation() => explanation.Value;
        MetadataNode<TMetadata> MetadataTier() => metadataTier.Value;
    }
}
