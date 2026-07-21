namespace Motiv.SyncToAsyncAdapter;

/// <summary>
/// Adapts a synchronous <see cref="PolicyBase{TModel,TMetadata}" /> into the asynchronous policy hierarchy,
/// preserving the single-value policy guarantee. Evaluation remains fully synchronous internally; results
/// are surfaced as already-completed ValueTasks so that the adapter can be composed with genuinely asynchronous
/// policies.
/// </summary>
/// <typeparam name="TModel">The model type that the policy will evaluate against.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata to associate with the predicate.</typeparam>
internal sealed class SyncPolicyAsyncAdapter<TModel, TMetadata>(
    PolicyBase<TModel, TMetadata> policy)
    : AsyncPolicyBase<TModel, TMetadata>, ISyncSpecAdapter
{
    private readonly SpecBase[] _underlying = [policy];

    /// <inheritdoc />
    public SpecBase UnderlyingSpec => policy;

    /// <inheritdoc />
    public override IEnumerable<SpecBase> Underlying => _underlying;

    /// <inheritdoc />
    public override ISpecDescription Description => policy.Description;

    /// <inheritdoc />
    public override ValueTask<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default)
    {
        try
        {
            return new ValueTask<bool>(policy.Matches(model));
        }
        catch (Exception ex)
        {
            return new ValueTask<bool>(Task.FromException<bool>(ex));
        }
    }

    /// <inheritdoc />
    public override AsyncSpecBase<TModel, string> ToAsyncExplanationSpec() =>
        this switch
        {
            AsyncSpecBase<TModel, string> explanationSpec => explanationSpec,
            _ => policy.ToExplanationSpec().ToAsyncSpec()
        };

    /// <inheritdoc />
    protected override ValueTask<PolicyResultBase<TMetadata>> EvaluatePolicyAsync(
        TModel model,
        CancellationToken cancellationToken)
    {
        try
        {
            return new ValueTask<PolicyResultBase<TMetadata>>(policy.EvaluatePolicyInternal(model));
        }
        catch (Exception ex)
        {
            return new ValueTask<PolicyResultBase<TMetadata>>(Task.FromException<PolicyResultBase<TMetadata>>(ex));
        }
    }
}
