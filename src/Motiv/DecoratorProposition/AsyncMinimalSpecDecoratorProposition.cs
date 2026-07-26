namespace Motiv.DecoratorProposition;

internal sealed class AsyncMinimalSpecDecoratorProposition<TModel, TMetadata>(
    AsyncSpecBase<TModel, TMetadata> underlyingSpec,
    ISpecDescription description)
    : AsyncSpecBase<TModel, TMetadata>
{
    private readonly SpecBase[] _underlying = [underlyingSpec];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => description;

    public override ValueTask<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default) =>
        underlyingSpec.MatchesAsync(model, cancellationToken);

    protected override async ValueTask<BooleanResultBase<TMetadata>> EvaluateSpecAsync(
        TModel model,
        CancellationToken cancellationToken)
    {
        var predicateResult = await underlyingSpec
            .EvaluateSpecAsyncInternal(model, cancellationToken)
            .ConfigureAwait(false);

        return new MinimalSpecDecoratorBooleanResult<TMetadata>(predicateResult, description);
    }
}
