using System.Threading;
using Motiv.Shared;

namespace Motiv.DecoratorProposition;

internal sealed class PolicyDecoratorMultiMetadataProposition<TModel, TMetadata, TUnderlyingMetadata>(
    PolicyBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse,
    ISpecDescription description)
    : SpecBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => underlyingSpec.ToEnumerable();

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => underlyingSpec.Matches(model);

    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(TModel model)
    {
        var booleanResult = underlyingSpec.IsSatisfiedBy(model);

        var metadata = new Lazy<IEnumerable<TMetadata>>(() =>
            booleanResult.Satisfied switch
            {
                true => whenTrue(model, booleanResult),
                false => whenFalse(model, booleanResult)
            }, LazyThreadSafetyMode.None);

        var assertions = new Lazy<string[]>(() =>
            metadata.Value switch
            {
                IEnumerable<string> because => because.ToArray(),
                _ => [Description.ToReason(booleanResult.Satisfied)]
            }, LazyThreadSafetyMode.None);

        return new BooleanResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
            booleanResult,
            () => new MetadataNode<TMetadata>(metadata.Value,
                booleanResult.ToEnumerable() as IEnumerable<BooleanResultBase<TMetadata>> ?? []),
            () => new Explanation(
                assertions.Value,
                booleanResult.ToEnumerable(),
                booleanResult.ToEnumerable()),
            () => new BooleanResultDescriptionWithUnderlying(
                booleanResult,
                Description.ToReason(booleanResult.Satisfied),
                Description.Statement));
    }
}
