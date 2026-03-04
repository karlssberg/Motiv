using System.Threading;
using Motiv.Shared;

namespace Motiv.DecoratorProposition;

internal sealed class SpecDecoratorMultiMetadataProposition<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse,
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

        var explanation = new Lazy<Explanation>(() =>
            new Explanation(
                assertions.Value,
                booleanResult.ToEnumerable(),
                booleanResult.ToEnumerable()), LazyThreadSafetyMode.None);

        var metadataTier = new Lazy<MetadataNode<TMetadata>>(() =>
            new MetadataNode<TMetadata>(metadata.Value,
                booleanResult.ToEnumerable() as IEnumerable<BooleanResultBase<TMetadata>> ?? []), LazyThreadSafetyMode.None);

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
            new BooleanResultDescriptionWithUnderlying(
                booleanResult,
                Description.ToReason(booleanResult.Satisfied),
                Description.Statement), LazyThreadSafetyMode.None);

        return new BooleanResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
            booleanResult,
            metadataTier,
            explanation,
            resultDescription);
    }
}
