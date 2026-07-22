namespace Motiv.DecoratorProposition;

internal sealed class AsyncSpecDecoratorWithSingleTrueAssertionProposition<TModel, TUnderlyingMetadata>(
    AsyncSpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    string trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> whenFalse,
    ISpecDescription description)
    : AsyncPolicyBase<TModel, string>
{
    private readonly SpecBase[] _underlying = [underlyingSpec];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => description;

    public override ValueTask<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default) =>
        underlyingSpec.MatchesAsync(model, cancellationToken);

    protected override async ValueTask<PolicyResultBase<string>> EvaluatePolicyAsync(
        TModel model,
        CancellationToken cancellationToken)
    {
        var underlyingResult = await underlyingSpec
            .EvaluateSpecAsyncInternal(model, cancellationToken)
            .ConfigureAwait(false);

        return new SpecDecoratorWithSingleTrueAssertionPolicyResult<TModel, TUnderlyingMetadata>(
            underlyingResult,
            model,
            trueBecause,
            whenFalse,
            description);
    }
}
