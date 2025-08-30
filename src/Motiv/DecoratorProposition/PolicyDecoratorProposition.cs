using Motiv.Shared;

namespace Motiv.DecoratorProposition;

internal sealed class PolicyDecoratorProposition<TModel, TMetadata, TUnderlyingMetadata>(
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

        var valueResolver =
            policyResult.Satisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        var lazyMetadata = new Lazy<TMetadata>(() => valueResolver(model, policyResult));

        var assertion = new Lazy<string>(() =>
            lazyMetadata.Value switch
            {
                string because => because,
                _ => Description.ToReason(policyResult.Satisfied)
            });

        var explanation = new Lazy<Explanation>(() =>
            new Explanation(
                assertion.Value,
                policyResult.ToEnumerable(),
                policyResult.ToEnumerable()));

        var metadataTier = new Lazy<MetadataNode<TMetadata>>(() =>
            new MetadataNode<TMetadata>(lazyMetadata.Value,
                policyResult.ToEnumerable() as IEnumerable<PolicyResultBase<TMetadata>> ?? []));

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
            new BooleanResultDescriptionWithUnderlying(
                policyResult,
                assertion.Value,
                Description.Statement));

        return new PolicyResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
            policyResult,
            () => lazyMetadata.Value,
            () => metadataTier.Value,
            () => explanation.Value,
            () => resultDescription.Value);
    }
}
