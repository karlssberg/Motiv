using Motiv.Shared;

namespace Motiv.DecoratorProposition;

internal sealed partial class SpecDecoratorProposition<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenFalse,
    ISpecDescription description)
    : PolicyBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => underlyingSpec.ToEnumerable();

    public override ISpecDescription Description => description;

    protected override PolicyResultBase<TMetadata> IsPolicySatisfiedBy(TModel model)
    {
        var booleanResult = underlyingSpec.IsSatisfiedBy(model);

        var metadataResolver =
            booleanResult.Satisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        var lazyMetadata = new Lazy<TMetadata>(() => metadataResolver(model, booleanResult));

        var assertion = new Lazy<string>(() =>
            lazyMetadata.Value switch
            {
                string because => because,
                _ => Description.ToReason(booleanResult.Satisfied)
            });

        var explanation = new Lazy<Explanation>(() =>
            new Explanation(
                assertion.Value,
                booleanResult.ToEnumerable(),
                booleanResult.ToEnumerable()));

        var metadataTier = new Lazy<MetadataNode<TMetadata>>(() =>
            new MetadataNode<TMetadata>(lazyMetadata.Value,
                booleanResult.ToEnumerable() as IEnumerable<BooleanResultBase<TMetadata>> ?? []));

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
            new BooleanResultDescriptionWithUnderlying(
                booleanResult,
                assertion.Value,
                Description.Statement));

        return new PolicyResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
            booleanResult,
            () => lazyMetadata.Value,
            () => metadataTier.Value,
            () => explanation.Value,
            () => resultDescription.Value);
    }
}
