using Motiv.Shared;

namespace Motiv.MetadataToExplanationAdapter;

internal sealed class MetadataToExplanationAdapterSpec<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec)
    : SpecBase<TModel, string>
{
    private readonly SpecBase[] _underlying = [spec];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => spec.Description;

    public override bool Matches(TModel model) => spec.Matches(model);

    protected override BooleanResultBase<string> EvaluateSpec(TModel model)
    {
        var result = spec.Evaluate(model);
        BooleanResultBase<TUnderlyingMetadata>[] results = [result];

        return new BooleanResultWithUnderlying<string, TUnderlyingMetadata>(
            result,
            () => new MetadataNode<string>(
                result.Assertions,
                results as IEnumerable<BooleanResultBase<string>> ?? []),
            () => result.Explanation,
            () => new BooleanResultDescriptionWithUnderlying(
                result,
                Description.ToReason(result.Satisfied),
                Description.Statement));
    }
}
