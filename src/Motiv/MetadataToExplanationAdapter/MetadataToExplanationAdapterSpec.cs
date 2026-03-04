using System.Threading;
using Motiv.Shared;

namespace Motiv.MetadataToExplanationAdapter;

internal sealed class MetadataToExplanationAdapterSpec<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec)
    : SpecBase<TModel, string>
{
    public override IEnumerable<SpecBase> Underlying => spec.ToEnumerable();

    public override ISpecDescription Description => spec.Description;

    public override bool Matches(TModel model) => spec.Matches(model);

    protected override BooleanResultBase<string> IsSpecSatisfiedBy(TModel model)
    {
        var result = spec.IsSatisfiedBy(model);

        var metadataTier = new Lazy<MetadataNode<string>>(() =>
            new MetadataNode<string>(
                result.Assertions,
                result.ToEnumerable() as IEnumerable<BooleanResultBase<string>> ?? []), LazyThreadSafetyMode.None);

        var description = new Lazy<ResultDescriptionBase>(() =>
            new BooleanResultDescriptionWithUnderlying(
                result,
                Description.ToReason(result.Satisfied),
                Description.Statement), LazyThreadSafetyMode.None);

        return new BooleanResultWithUnderlying<string, TUnderlyingMetadata>(
            result,
            metadataTier,
            new Lazy<Explanation>(() => result.Explanation, LazyThreadSafetyMode.None),
            description);
    }
}
