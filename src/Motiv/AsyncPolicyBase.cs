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
    protected override async Task<BooleanResultBase<TMetadata>> EvaluateSpecAsync(
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
    public new Task<PolicyResultBase<TMetadata>> EvaluateAsync(TModel model, CancellationToken cancellationToken = default) =>
        EvaluatePolicyAsync(model, cancellationToken);

    /// <summary>
    /// Asynchronously evaluates the policy against the model.
    /// </summary>
    /// <param name="model">The model to evaluate the policy against.</param>
    /// <param name="cancellationToken">A token that can cancel the evaluation.</param>
    /// <returns>A result that contains the Boolean result of the predicate in addition to the metadata.</returns>
    protected abstract Task<PolicyResultBase<TMetadata>> EvaluatePolicyAsync(TModel model, CancellationToken cancellationToken);
}
