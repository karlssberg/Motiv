using System.Threading;
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

    public override bool Matches(TModel model) => underlyingSpec.Matches(model);

    protected override PolicyResultBase<TMetadata> IsPolicySatisfiedBy(TModel model)
    {
        var policyResult = underlyingSpec.IsSatisfiedBy(model);

        var valueResolver =
            policyResult.Satisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        var lazyMetadata = new Lazy<TMetadata>(() => valueResolver(model, policyResult), LazyThreadSafetyMode.None);

        var assertion = new Lazy<string>(() =>
            lazyMetadata.Value switch
            {
                string because => because,
                _ => Description.ToReason(policyResult.Satisfied)
            }, LazyThreadSafetyMode.None);

        return new PolicyResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
            policyResult,
            () => lazyMetadata.Value,
            () => new MetadataNode<TMetadata>(lazyMetadata.Value,
                policyResult.ToEnumerable() as IEnumerable<PolicyResultBase<TMetadata>> ?? []),
            () => new Explanation(
                assertion.Value,
                policyResult.ToEnumerable(),
                policyResult.ToEnumerable()),
            () => new BooleanResultDescriptionWithUnderlying(
                policyResult,
                assertion.Value,
                Description.Statement));
    }
}
