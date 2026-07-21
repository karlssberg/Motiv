namespace Motiv.SyncToAsyncAdapter;

/// <summary>
/// Adapts a synchronous <see cref="SpecBase{TModel,TMetadata}" /> into the asynchronous specification
/// hierarchy. Evaluation remains fully synchronous internally; results are surfaced as already-completed
/// ValueTasks so that the adapter can be composed with genuinely asynchronous specifications.
/// </summary>
/// <typeparam name="TModel">The model type that the specification will evaluate against.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata to associate with the predicate.</typeparam>
internal sealed class SyncSpecAsyncAdapter<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> spec)
    : AsyncSpecBase<TModel, TMetadata>, ISyncSpecAdapter
{
    private readonly SpecBase[] _underlying = [spec];

    /// <inheritdoc />
    public SpecBase UnderlyingSpec => spec;

    /// <inheritdoc />
    public override IEnumerable<SpecBase> Underlying => _underlying;

    /// <inheritdoc />
    public override ISpecDescription Description => spec.Description;

    /// <inheritdoc />
    public override ValueTask<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default)
    {
        try
        {
            return new ValueTask<bool>(spec.Matches(model));
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
            _ => spec.ToExplanationSpec().ToAsyncSpec()
        };

    /// <inheritdoc />
    protected override ValueTask<BooleanResultBase<TMetadata>> EvaluateSpecAsync(
        TModel model,
        CancellationToken cancellationToken)
    {
        try
        {
            return new ValueTask<BooleanResultBase<TMetadata>>(spec.EvaluateInternal(model));
        }
        catch (Exception ex)
        {
            return new ValueTask<BooleanResultBase<TMetadata>>(Task.FromException<BooleanResultBase<TMetadata>>(ex));
        }
    }
}
