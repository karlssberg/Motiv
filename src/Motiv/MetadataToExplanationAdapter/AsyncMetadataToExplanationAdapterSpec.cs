namespace Motiv.MetadataToExplanationAdapter;

internal sealed class AsyncMetadataToExplanationAdapterSpec<TModel, TUnderlyingMetadata>(
    AsyncSpecBase<TModel, TUnderlyingMetadata> spec)
    : AsyncSpecBase<TModel, string>
{
    private readonly SpecBase[] _underlying = [spec];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => spec.Description;

    public override Task<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default) =>
        spec.MatchesAsync(model, cancellationToken);

    protected override async Task<BooleanResultBase<string>> EvaluateSpecAsync(
        TModel model,
        CancellationToken cancellationToken)
    {
        var result = await spec.EvaluateAsync(model, cancellationToken).ConfigureAwait(false);

        return new MetadataToExplanationAdapterBooleanResult<TUnderlyingMetadata>(result, spec.Description);
    }
}
