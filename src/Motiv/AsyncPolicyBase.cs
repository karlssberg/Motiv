using Motiv.Diagnostics;
using Motiv.Not;
using Motiv.OrElse;

namespace Motiv;

/// <summary>
/// The base class for asynchronous policies. A policy is a specification that resolves to a single value —
/// one assertion or one metadata object per evaluation. Mirrors <see cref="PolicyBase{TModel, TMetadata}" />.
/// </summary>
/// <typeparam name="TModel">The model type that the policy will evaluate against</typeparam>
/// <typeparam name="TMetadata">The type of the metadata to associate with the predicate</typeparam>
public abstract class AsyncPolicyBase<TModel, TMetadata> : AsyncSpecBase<TModel, TMetadata>
{
    /// <inheritdoc />
    protected override async ValueTask<BooleanResultBase<TMetadata>> EvaluateSpecAsync(
        TModel model,
        CancellationToken cancellationToken) =>
        await EvaluatePolicyAsync(model, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Asynchronously evaluates the policy against the model and returns a result that contains the Boolean
    /// result of the predicate in addition to a single metadata value.
    /// </summary>
    /// <param name="model">The model to evaluate the policy against.</param>
    /// <param name="cancellationToken">A token that can cancel the evaluation.</param>
    /// <returns>A result that contains the Boolean result of the predicate in addition to the metadata.</returns>
    public new ValueTask<PolicyResultBase<TMetadata>> EvaluateAsync(
        TModel model,
        CancellationToken cancellationToken = default) =>
        MotivTelemetry.IsEnabled
            ? EvaluatePolicyInstrumentedAsync(model, cancellationToken)
            : EvaluatePolicyAsync(model, cancellationToken);

    /// <summary>
    /// Kept separate from <see cref="EvaluateAsync(TModel,CancellationToken)" /> so that the public boundary
    /// remains free of an async state machine when telemetry is disabled.
    /// </summary>
    private async ValueTask<PolicyResultBase<TMetadata>> EvaluatePolicyInstrumentedAsync(
        TModel model,
        CancellationToken cancellationToken)
    {
        var scope = EvaluationScope.Start(Description.Statement);
        PolicyResultBase<TMetadata> result;
        try
        {
            result = await EvaluatePolicyAsync(model, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller's own token was signalled, so this cancellation is intentional, not a failure — see
            // EvaluationScope.Cancel. An OperationCanceledException without the caller's token signalled (an
            // internal timeout, a foreign token, a bug) falls through to the catch below and is still an error.
            // Guarded like Complete below: a throwing listener must not replace the cancellation exception.
            try { scope.Cancel(); } catch { }
            throw;
        }
        catch (Exception exception)
        {
            // Guarded for the same reason as Complete below: Fail disposes the activity, which synchronously
            // runs listener callbacks — a throwing listener must not replace the evaluation's own exception.
            try { scope.Fail(exception); } catch { }
            throw;
        }

        try
        {
            scope.Complete(result);
        }
        catch
        {
            // A listener or exporter failing while consuming the completed scope (e.g. a throwing
            // ActivityStopped callback invoked synchronously by Activity.Dispose()) belongs to telemetry, not
            // the evaluation: this call already succeeded, so that failure must not be attributed to it as an
            // error, nor allowed to escape — deliberately not scope.Fail(...).
        }

        return result;
    }

    /// <summary>
    /// Asynchronously evaluates the policy without emitting telemetry. Used by composite and adapter
    /// propositions when evaluating their operands, so that a composed proposition emits a single span at its
    /// root rather than one per node.
    /// </summary>
    /// <param name="model">The model to evaluate the policy against.</param>
    /// <param name="cancellationToken">A token that can cancel the evaluation.</param>
    /// <returns>A result that contains the Boolean result of the predicate in addition to the metadata.</returns>
    internal ValueTask<PolicyResultBase<TMetadata>> EvaluatePolicyAsyncInternal(
        TModel model,
        CancellationToken cancellationToken) =>
        EvaluatePolicyAsync(model, cancellationToken);

    /// <summary>
    /// Asynchronously evaluates the policy against the model.
    /// </summary>
    /// <param name="model">The model to evaluate the policy against.</param>
    /// <param name="cancellationToken">A token that can cancel the evaluation.</param>
    /// <returns>A result that contains the Boolean result of the predicate in addition to the metadata.</returns>
    protected abstract ValueTask<PolicyResultBase<TMetadata>> EvaluatePolicyAsync(TModel model, CancellationToken cancellationToken);

    /// <summary>
    /// Combines this policy with another policy using the conditional OR operator, preserving the policy
    /// guarantee. The right operand is only evaluated if the left operand resolves to <c>false</c>.
    /// </summary>
    /// <param name="alternative">The fallback policy.</param>
    /// <returns>A new policy that represents the conditional OR of this policy and the alternative.</returns>
    public AsyncPolicyBase<TModel, TMetadata> OrElse(AsyncPolicyBase<TModel, TMetadata> alternative) =>
        new AsyncOrElsePolicy<TModel, TMetadata>(this, alternative);

    /// <summary>
    /// Combines this policy with a synchronous policy using the conditional OR operator, preserving the
    /// policy guarantee. The synchronous alternative is lifted into the asynchronous hierarchy via
    /// <see cref="PolicyBase{TModel,TMetadata}.ToAsyncSpec" />. The right operand is only evaluated if the
    /// left operand resolves to <c>false</c>.
    /// </summary>
    /// <param name="alternative">The synchronous fallback policy.</param>
    /// <returns>A new policy that represents the conditional OR of this policy and the alternative.</returns>
    public AsyncPolicyBase<TModel, TMetadata> OrElse(PolicyBase<TModel, TMetadata> alternative) =>
        OrElse(alternative.ToAsyncSpec());

    /// <summary>Negates this policy.</summary>
    /// <returns>A new policy that represents the logical NOT of this policy.</returns>
    public new AsyncPolicyBase<TModel, TMetadata> Not() => new AsyncNotPolicy<TModel, TMetadata>(this);

    /// <summary>Negates a policy.</summary>
    /// <param name="policy">The policy to negate.</param>
    /// <returns>A new policy that represents the logical NOT of the policy.</returns>
    public static AsyncPolicyBase<TModel, TMetadata> operator !(AsyncPolicyBase<TModel, TMetadata> policy) =>
        policy.Not();
}
