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
    private readonly SpecBase[] _underlying = [underlyingSpec];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => underlyingSpec.Matches(model);

    protected override BooleanResultBase<TMetadata> EvaluateSpec(TModel model)
    {
        var booleanResult = underlyingSpec.Evaluate(model);
        PolicyResultBase<TUnderlyingMetadata>[] booleanResults = [booleanResult];

        var metadata = new Lazy<IEnumerable<TMetadata>>(() =>
            booleanResult.Satisfied switch
            {
                true => whenTrue(model, booleanResult),
                false => whenFalse(model, booleanResult)
            }, LazyThreadSafetyMode.None);

        var assertions = new Lazy<string[]>(() =>
            [Description.ToReason(booleanResult.Satisfied)], LazyThreadSafetyMode.None);

        return new BooleanResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
            booleanResult,
            () => new MetadataNode<TMetadata>(metadata.Value,
                booleanResults as IEnumerable<BooleanResultBase<TMetadata>> ?? []),
            () => new Explanation(
                assertions.Value,
                booleanResults,
                booleanResults),
            () => new BooleanResultDescriptionWithUnderlying(
                booleanResult,
                Description.ToReason(booleanResult.Satisfied),
                Description.Statement));
    }
}
