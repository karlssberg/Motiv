namespace Motiv.Traversal;

/// <summary>
/// The concurrent operators' evaluation — a fan-out rather than a walk. <c>AsyncAndSpec</c>,
/// <c>AsyncOrSpec</c> and <c>AsyncXOrSpec</c> take a <c>concurrent</c> flag that evaluates both operands
/// through <see cref="Task.WhenAll(Task[])" />, so the fold leaves such a node to evaluate itself.
/// </summary>
/// <remarks>
/// Written once rather than six times because of what it now has to carry. The three operators had a
/// copy each on both the result and the match path, all structurally identical and differing only in
/// how the two answers are combined — and
/// <see href="https://github.com/karlssberg/Motiv/issues/204">#204</see> gave them something to agree
/// about: the fan-out is the one asynchronous shape no fold sits above, so it is where the evaluation's
/// budget has to be established for the two branches to share one.
/// <para>
/// The scope is claimed before either operand is started, in this method's synchronous prefix, so that
/// the counter is in the execution context <see cref="Task.WhenAll(Task[])" /> flows into both branches.
/// See <see cref="EvaluationBudget.EnterFanOut" /> for why this entry charges the node only when nothing
/// reached it.
/// </para>
/// </remarks>
internal static class AsyncConcurrentFanOut
{
    /// <summary>Evaluates both operands at once, returning the pair for the operator to combine.</summary>
    internal static async ValueTask<(BooleanResultBase<TMetadata> Left, BooleanResultBase<TMetadata> Right)>
        EvaluateBothAsync<TModel, TMetadata>(
            AsyncSpecBase<TModel, TMetadata> left,
            AsyncSpecBase<TModel, TMetadata> right,
            TModel model,
            CancellationToken cancellationToken)
    {
        using var budget = EvaluationBudget.EnterFanOut();

        var leftTask = left.EvaluateSpecAsyncInternal(model, cancellationToken).AsTask();
        var rightTask = right.EvaluateSpecAsyncInternal(model, cancellationToken).AsTask();
        await Task.WhenAll(leftTask, rightTask).ConfigureAwait(false);

        return (await leftTask.ConfigureAwait(false), await rightTask.ConfigureAwait(false));
    }

    /// <summary>The same on the outcome-only path, which composes no results.</summary>
    internal static async ValueTask<(bool Left, bool Right)> MatchBothAsync<TModel, TMetadata>(
        AsyncSpecBase<TModel, TMetadata> left,
        AsyncSpecBase<TModel, TMetadata> right,
        TModel model,
        CancellationToken cancellationToken)
    {
        using var budget = EvaluationBudget.EnterFanOut();

        var leftTask = left.MatchesAsync(model, cancellationToken).AsTask();
        var rightTask = right.MatchesAsync(model, cancellationToken).AsTask();
        await Task.WhenAll(leftTask, rightTask).ConfigureAwait(false);

        return (await leftTask.ConfigureAwait(false), await rightTask.ConfigureAwait(false));
    }
}
