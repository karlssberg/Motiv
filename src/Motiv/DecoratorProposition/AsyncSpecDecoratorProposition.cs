namespace Motiv.DecoratorProposition;

internal sealed class AsyncSpecDecoratorProposition<TModel, TMetadata, TUnderlyingMetadata>(
    AsyncSpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenFalse,
    ISpecDescription description)
    : AsyncPolicyBase<TModel, TMetadata>
{
    private readonly SpecBase[] _underlying = [underlyingSpec];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => description;

    public override ValueTask<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default) =>
        underlyingSpec.MatchesAsync(model, cancellationToken);

    protected override async ValueTask<PolicyResultBase<TMetadata>> EvaluatePolicyAsync(
        TModel model,
        CancellationToken cancellationToken)
    {
        var booleanResult = await underlyingSpec
            .EvaluateSpecAsyncInternal(model, cancellationToken)
            .ConfigureAwait(false);

        var metadataResolver =
            booleanResult.Satisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        return new SpecDecoratorPolicyResult<TModel, TMetadata, TUnderlyingMetadata>(
            booleanResult,
            model,
            metadataResolver,
            description);
    }
}
