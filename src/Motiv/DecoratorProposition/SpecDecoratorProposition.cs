using System.Threading;
using Motiv.Shared;

namespace Motiv.DecoratorProposition;

internal sealed class SpecDecoratorProposition<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenFalse,
    ISpecDescription description)
    : PolicyBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => underlyingSpec.ToEnumerable();

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => underlyingSpec.Matches(model);

    protected override PolicyResultBase<TMetadata> IsPolicySatisfiedBy(TModel model)
    {
        var booleanResult = underlyingSpec.IsSatisfiedBy(model);

        var metadataResolver =
            booleanResult.Satisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        var lazyMetadata = new Lazy<TMetadata>(() => metadataResolver(model, booleanResult), LazyThreadSafetyMode.None);

        var assertion = new Lazy<string>(() =>
            lazyMetadata.Value switch
            {
                string because => because,
                _ => Description.ToReason(booleanResult.Satisfied)
            }, LazyThreadSafetyMode.None);

        var explanation = new Lazy<Explanation>(() =>
            new Explanation(
                assertion.Value,
                booleanResult.ToEnumerable(),
                booleanResult.ToEnumerable()), LazyThreadSafetyMode.None);

        var metadataTier = new Lazy<MetadataNode<TMetadata>>(() =>
            new MetadataNode<TMetadata>(lazyMetadata.Value,
                booleanResult.ToEnumerable() as IEnumerable<BooleanResultBase<TMetadata>> ?? []), LazyThreadSafetyMode.None);

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
            new BooleanResultDescriptionWithUnderlying(
                booleanResult,
                assertion.Value,
                Description.Statement), LazyThreadSafetyMode.None);

        return new PolicyResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
            booleanResult,
            lazyMetadata,
            metadataTier,
            explanation,
            resultDescription);
    }
}
