namespace Motiv.SpecDecoratorProposition;

internal sealed class SpecDecoratorMetadataPolicy<TModel, TMetadata, TUnderlyingMetadata>(
    PolicyBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, TMetadata> whenTrue,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, TMetadata> whenFalse,
    ISpecDescription description)
    : PolicyBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => underlyingSpec.ToEnumerable();


    public override ISpecDescription Description => description;

    protected override PolicyResultBase<TMetadata> IsPolicySatisfiedBy(TModel model)
    {
        var policyResult = underlyingSpec.IsSatisfiedBy(model);
        var lazyMetadata = CreateLazyMetadata(model, policyResult);

        return CreatePolicyResult(lazyMetadata, policyResult);
    }

    private Lazy<TMetadata> CreateLazyMetadata(TModel model, PolicyResultBase<TUnderlyingMetadata> policyResult)
    {
        return new Lazy<TMetadata>(() =>
            policyResult.Satisfied switch
            {
                true => whenTrue(model, policyResult),
                false => whenFalse(model, policyResult)
            });
    }

    private PolicyResultBase<TMetadata> CreatePolicyResult(Lazy<TMetadata> metadata, PolicyResultBase<TUnderlyingMetadata> policyResult)
    {
        var assertions = new Lazy<string[]>(() =>
            metadata.Value switch
            {
                IEnumerable<string> because => because.ToArray(),
                _ => [Description.ToReason(policyResult.Satisfied)]
            });

        var explanation = new Lazy<Explanation>(() =>
            new Explanation(assertions.Value, policyResult.ToEnumerable()));

        var metadataTier = new Lazy<MetadataNode<TMetadata>>(() =>
            new MetadataNode<TMetadata>(metadata.Value,
                policyResult.ToEnumerable() as IEnumerable<PolicyResultBase<TMetadata>> ?? []));

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
            new BooleanResultDescriptionWithUnderlying(
                policyResult,
                Description.ToReason(policyResult.Satisfied),
                Description.Statement));

        return new PolicyResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
            policyResult,
            Value,
            MetadataTier,
            Explanation,
            ResultDescription);

        TMetadata Value() => metadata.Value;
        Explanation Explanation() => explanation.Value;
        MetadataNode<TMetadata> MetadataTier() => metadataTier.Value;
        ResultDescriptionBase ResultDescription() => resultDescription.Value;
    }
}
