namespace Motiv.DecoratorProposition;

internal sealed class PolicyDecoratorMultiMetadataProposition<TModel, TMetadata, TUnderlyingMetadata>(
    PolicyBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse,
    ISpecDescription description)
    : SpecBase<TModel, TMetadata>
{
    private readonly SpecBase[] _underlying = [underlyingSpec];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => underlyingSpec.Matches(model);

    protected override BooleanResultBase<TMetadata> EvaluateSpec(TModel model)
    {
        var policyResult = underlyingSpec.Evaluate(model);

        var metadataResolver =
            policyResult.Satisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        return new PolicyDecoratorMultiMetadataBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
            policyResult,
            model,
            metadataResolver,
            description);
    }
}
