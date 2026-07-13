namespace Motiv.BooleanPredicateProposition;

/// <summary>
/// An asynchronous explanation proposition whose statement is derived from the WhenTrue assertion. The
/// because-strings double as the explanation; degenerate (null/empty/whitespace) strings fall back to the
/// statement-derived reason.
/// </summary>
internal sealed class AsyncExplanationProposition<TModel>(
    Func<TModel, CancellationToken, Task<bool>> predicate,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause,
    ISpecDescription specDescription)
    : AsyncPolicyBase<TModel, string>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    public override async Task<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default) =>
        await predicate(model, cancellationToken).ConfigureAwait(false);

    protected override async Task<PolicyResultBase<string>> EvaluatePolicyAsync(
        TModel model,
        CancellationToken cancellationToken)
    {
        var isSatisfied = await predicate(model, cancellationToken).ConfigureAwait(false);

        var becauseResolver =
            isSatisfied switch
            {
                true => trueBecause,
                false => falseBecause
            };

        return new ExplanationPropositionPolicyResult<TModel>(
            isSatisfied,
            model,
            becauseResolver,
            specDescription);
    }
}
