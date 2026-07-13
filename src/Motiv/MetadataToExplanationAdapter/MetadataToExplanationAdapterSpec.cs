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
        var result = spec.EvaluateInternal(model);

        return new MetadataToExplanationAdapterBooleanResult<TUnderlyingMetadata>(result, spec.Description);
    }
}
