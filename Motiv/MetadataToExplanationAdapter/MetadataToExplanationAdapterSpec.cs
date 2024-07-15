namespace Motiv.MetadataToExplanationAdapter;

internal sealed class MetadataToExplanationAdapterSpec<TModel, TUnderlyingModel>(
    SpecBase<TModel, TUnderlyingModel> spec)
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

        return new BooleanResultWithUnderlying<string, TUnderlyingModel>(
            result,
            MetadataTier,
            Explanation,
            Reason);

        MetadataNode<string> MetadataTier() => metadataTier.Value;
        Explanation Explanation() => result.Explanation;
        string Reason() => spec.Description.ToReason(result.Satisfied);
    }
}
