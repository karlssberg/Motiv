namespace Motiv.MetadataToExplanationAdapter;

internal sealed class MetadataToExplanationAdapterSpec<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec)
    : SpecBase<TModel, string>
{
    public override IEnumerable<SpecBase> Underlying => spec.ToEnumerable();

    public override ISpecDescription Description => spec.Description;

    protected override BooleanResultBase<string> IsSpecSatisfiedBy(TModel model)
    {
        var result = spec.IsSatisfiedBy(model);

        var metadataTier = new Lazy<MetadataNode<string>>(() =>
            new MetadataNode<string>(
                result.Assertions,
                result.ToEnumerable() as IEnumerable<BooleanResultBase<string>> ?? []));

        var description = new Lazy<ResultDescriptionBase>(() =>
            new BooleanResultDescriptionWithUnderlying(
                result,
                Description.ToReason(result.Satisfied),
                Description.Statement));

        return new BooleanResultWithUnderlying<string, TUnderlyingMetadata>(
            result,
            MetadataTier,
            Explanation,
            ResultDescription);

        MetadataNode<string> MetadataTier() => metadataTier.Value;
        Explanation Explanation() => result.Explanation;
        ResultDescriptionBase ResultDescription() => description.Value;
    }
}
