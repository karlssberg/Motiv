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
    private readonly SpecBase[] _underlying = [underlyingSpec];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => underlyingSpec.Matches(model);

    protected override PolicyResultBase<TMetadata> IsPolicySatisfiedBy(TModel model)
    {
        var booleanResult = underlyingSpec.IsSatisfiedBy(model);
        BooleanResultBase<TUnderlyingMetadata>[] booleanResults = [booleanResult];

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

        return new PolicyResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
            booleanResult,
            () => lazyMetadata.Value,
            () => new MetadataNode<TMetadata>(lazyMetadata.Value,
                booleanResults as IEnumerable<BooleanResultBase<TMetadata>> ?? []),
            () => new Explanation(
                assertion.Value,
                booleanResults,
                booleanResults),
            () => new BooleanResultDescriptionWithUnderlying(
                booleanResult,
                assertion.Value,
                Description.Statement));
    }
}
