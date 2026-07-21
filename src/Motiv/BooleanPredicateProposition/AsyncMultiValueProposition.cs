namespace Motiv.BooleanPredicateProposition;

/// <summary>
/// Represents an asynchronous predicate that when evaluated returns a boolean result with associated
/// metadata collection and description of the underlying proposition that were responsible for the result.
/// </summary>
/// <typeparam name="TModel">The type of the input parameter.</typeparam>
/// <typeparam name="TMetadata">The type of the return value.</typeparam>
internal sealed class AsyncMultiValueProposition<TModel, TMetadata>(
    Func<TModel, CancellationToken, ValueTask<bool>> predicate,
    Func<TModel, IEnumerable<TMetadata>> whenTrue,
    Func<TModel, IEnumerable<TMetadata>> whenFalse,
    ISpecDescription specDescription)
    : AsyncSpecBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    public override async ValueTask<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default) =>
        await predicate(model, cancellationToken).ConfigureAwait(false);

    protected override async ValueTask<BooleanResultBase<TMetadata>> EvaluateSpecAsync(
        TModel model,
        CancellationToken cancellationToken)
    {
        var isSatisfied = await predicate(model, cancellationToken).ConfigureAwait(false);

        var metadataResolver =
            isSatisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        return new MultiValuePropositionBooleanResult<TModel, TMetadata>(
            isSatisfied,
            model,
            metadataResolver,
            specDescription);
    }
}
