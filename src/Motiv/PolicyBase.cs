using Motiv.ChangeModelType;
using Motiv.Diagnostics;
using Motiv.Not;
using Motiv.OrElse;
using Motiv.SyncToAsyncAdapter;

namespace Motiv;

/// <summary>
/// Represents a "policy" whereby an arbitrary rule causes a single metadata instance to be returned for both the
/// true and false conditions.
/// </summary>
/// <typeparam name="TModel">The model type that the policy is for.</typeparam>
/// <typeparam name="TMetadata">The metadata type that the policy returns.</typeparam>
public abstract class PolicyBase<TModel, TMetadata> : SpecBase<TModel, TMetadata>
{
    /// <inheritdoc />
    protected override BooleanResultBase<TMetadata> EvaluateSpec(TModel model) => EvaluatePolicy(model);

    /// <summary>
    /// Executes the proposition as a policy and returns a <see cref="PolicyResultBase{TMetadata}" /> primarily
    /// containing a single metadata instance.
    /// </summary>
    /// <param name="model">The model to evaluate</param>
    /// <returns>A <see cref="PolicyResultBase{TMetadata}" /> containing the metadata instance and the boolean result.</returns>
    public new PolicyResultBase<TMetadata> Evaluate(TModel model) =>
        MotivTelemetry.IsEnabled ? EvaluatePolicyInstrumented(model) : EvaluatePolicy(model);

    /// <summary>
    /// Kept separate from <see cref="Evaluate(TModel)" /> so that the public boundary remains a small, inlineable
    /// method when telemetry is disabled — a method containing a try/catch cannot be inlined by the JIT.
    /// </summary>
    private PolicyResultBase<TMetadata> EvaluatePolicyInstrumented(TModel model)
    {
        var scope = EvaluationScope.Start(Description.Statement);
        PolicyResultBase<TMetadata> result;
        try
        {
            result = EvaluatePolicy(model);
        }
        catch (Exception exception)
        {
            scope.Fail(exception);
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
    /// Evaluates the policy without emitting telemetry. Used by composite, decorator and higher-order
    /// propositions when evaluating their operands, so that a composed proposition emits a single span at its
    /// root rather than one per node.
    /// </summary>
    /// <param name="model">The model to evaluate the policy against.</param>
    /// <returns>A result containing the metadata instance and the boolean result.</returns>
    internal PolicyResultBase<TMetadata> EvaluatePolicyInternal(TModel model) => EvaluatePolicy(model);

    /// <inheritdoc cref="Evaluate(TModel)"/>
    [Obsolete("Use Evaluate instead.")]
    public new PolicyResultBase<TMetadata> IsSatisfiedBy(TModel model) => Evaluate(model);

    /// <summary>
    /// Executes the proposition as a policy and returns a <see cref="PolicyResultBase{TMetadata}" /> primarily
    /// containing a single metadata instance.
    /// </summary>
    /// <param name="model">The model to evaluate</param>
    /// <returns>A <see cref="PolicyResultBase{TMetadata}" /> containing the metadata instance and the boolean result.</returns>
    protected abstract PolicyResultBase<TMetadata> EvaluatePolicy(TModel model);

    /// <summary>Changes the <typeparamref name="TModel" /> <see cref="Type" /> of the policy.</summary>
    /// <param name="childModelSelector">
    /// A function that takes the model and returns the child model to evaluate the
    /// specification against.
    /// </param>
    /// <typeparam name="TNewModel"></typeparam>
    /// <returns>
    /// A new policy that represents the same policy but with a different <typeparamref name="TModel" />.
    /// </returns>
    public new PolicyBase<TNewModel, TMetadata> ChangeModelTo<TNewModel>(
        Func<TNewModel, TModel> childModelSelector) =>
        new ChangeModelTypePolicy<TNewModel, TModel, TMetadata>(
            this,
            childModelSelector.ThrowIfNull(nameof(childModelSelector)));

    /// <summary>Changes the <typeparamref name="TModel" /> <see cref="Type" /> of the policy.</summary>
    /// <typeparam name="TDerivedModel">
    /// The type to change the <typeparamref name="TModel" /> to. This type must be a subclass
    /// of <typeparamref name="TModel" />.
    /// </typeparam>
    /// <returns>
    /// A new policy that represents the same policy but with a different <typeparamref name="TModel" />.
    /// </returns>
    public new PolicyBase<TDerivedModel, TMetadata> ChangeModelTo<TDerivedModel>()
        where TDerivedModel : TModel =>
        new ChangeModelTypePolicy<TDerivedModel, TModel, TMetadata>(this, model => model);

    /// <summary>
    /// Creates a new policy that is equivalent to a conditional "OR" of the current policy and the alternative
    /// policy. In the event that neither policy is satisfied, the alternative policy's "WhenFalse" metadata is selected as the
    /// policy value.
    /// </summary>
    /// <param name="alternative">The policy to evaluate in the event that <c>this</c> policy is unsatisfied</param>
    /// <returns>
    /// A new <see cref="PolicyBase{TModel,TMetadata}" /> that will perform the "Else" operation between <c>this</c>
    /// and <paramref name="alternative" /> when the policy is eventually evaluated.
    /// </returns>
    public PolicyBase<TModel, TMetadata> OrElse(PolicyBase<TModel, TMetadata> alternative) =>
        new OrElsePolicy<TModel, TMetadata>(this, alternative);

    /// <summary>
    /// Creates a new asynchronous policy that is equivalent to a conditional "OR" of the current policy and
    /// the asynchronous alternative policy, preserving the single-value policy guarantee. This policy is
    /// lifted into the asynchronous hierarchy via <see cref="ToAsyncSpec" />. In the event that neither policy
    /// is satisfied, the alternative policy's "WhenFalse" metadata is selected as the policy value.
    /// </summary>
    /// <param name="alternative">The asynchronous policy to evaluate in the event that <c>this</c> policy is unsatisfied</param>
    /// <returns>
    /// A new <see cref="AsyncPolicyBase{TModel,TMetadata}" /> that will perform the "Else" operation between
    /// <c>this</c> and <paramref name="alternative" /> when the policy is eventually evaluated.
    /// </returns>
    public AsyncPolicyBase<TModel, TMetadata> OrElse(AsyncPolicyBase<TModel, TMetadata> alternative) =>
        ToAsyncSpec().OrElse(alternative);

    /// <summary>Creates a new policy that is equivalent to a logical "NOT" of the current policy.</summary>
    /// <returns>A new <see cref="PolicyBase{TModel,TMetadata}" /> that will perform the "Not" operation when evaluated.</returns>
    public new PolicyBase<TModel, TMetadata> Not() => new NotPolicy<TModel, TMetadata>(this);

    /// <summary>Creates a new policy that is equivalent to a logical "NOT" of the current policy.</summary>
    /// <param name="policy">The policy to negate</param>
    /// <returns>A new <see cref="PolicyBase{TModel,TMetadata}" /> that will perform the "Not" operation when evaluated.</returns>
    public static PolicyBase<TModel, TMetadata> operator !(PolicyBase<TModel, TMetadata> policy) => policy.Not();

    /// <summary>
    /// Lifts this synchronous policy into the asynchronous hierarchy, preserving the single-value policy
    /// guarantee. Evaluation remains fully synchronous internally.
    /// </summary>
    /// <returns>An asynchronous view over this policy.</returns>
    public new AsyncPolicyBase<TModel, TMetadata> ToAsyncSpec() =>
        (AsyncPolicyBase<TModel, TMetadata>)base.ToAsyncSpec();

    private protected override AsyncSpecBase<TModel, TMetadata> CreateAsyncAdapter() =>
        new SyncPolicyAsyncAdapter<TModel, TMetadata>(this);
}
