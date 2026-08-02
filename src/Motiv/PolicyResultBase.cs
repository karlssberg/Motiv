using Motiv.AndAlso;
using Motiv.Not;
using Motiv.OrElse;

namespace Motiv;

/// <summary>Represents a "policy result" whereby an arbitrary rule causes a single metadata instance to be returned.</summary>
/// <typeparam name="TMetadata"></typeparam>
public abstract class PolicyResultBase<TMetadata> : BooleanResultBase<TMetadata>
{
    /// <summary>The single metadata instance that is returned by the policy.</summary>
    /// <remarks>
    /// For a short-circuiting composition (<see cref="OrElse" /> or <see cref="AndAlso" />) this value is a
    /// <em>selection</em> — the last-evaluated operand's — and not a guarantee that only one cause exists.
    /// When such a composition has more than one contributing cause, <see cref="BooleanResultBase{TMetadata}.Values" />
    /// reports all of them, so <c>Value</c> is not necessarily <c>Values.Single()</c>.
    /// </remarks>
    public abstract TMetadata Value { get; }

    /// <summary>
    /// Performs a conditional AND operation between the current PolicyResultBase instance and another
    /// PolicyResultBase instance. The right operand does not contribute when the left operand is unsatisfied,
    /// since an unsatisfied left operand already determines the outcome.
    /// </summary>
    /// <param name="right">The other policy result instance to perform the AND operation with.</param>
    /// <returns>A new policy result instance representing the result of the AND operation.</returns>
    public PolicyResultBase<TMetadata> AndAlso(PolicyResultBase<TMetadata> right) => Satisfied
        ? new AndAlsoPolicyResult<TMetadata>(this, right)
        : new AndAlsoPolicyResult<TMetadata>(this);

    /// <summary>
    /// Performs a conditional OR operation between the current PolicyResultBase instance and another PolicyResultBase
    /// instance. This will short-circuit the evaluation of the right operand if the left operand is satisfied.
    /// </summary>
    /// <param name="right">The other policy result instance to perform the OR operation with.</param>
    /// <returns>A new policy result instance representing the result of the OR operation.</returns>
    public PolicyResultBase<TMetadata> OrElse(PolicyResultBase<TMetadata> right) => Satisfied
        ? new OrElsePolicyResult<TMetadata>(this)
        : new OrElsePolicyResult<TMetadata>(this, right);

    /// <summary>
    /// Returns a new instance of <see cref="NotPolicyResult{TMetadata}" /> that represents the logical negation of
    /// the current instance.
    /// </summary>
    /// <returns>
    /// A new instance of <see cref="NotBooleanOperationResult{TMetadata}" /> that represents the logical negation of
    /// the current instance.
    /// </returns>
    public new PolicyResultBase<TMetadata> Not() => new NotPolicyResult<TMetadata>(this);

    /// <summary>
    /// Creates a new <see cref="PolicyResultBase{TMetadata}" /> that is equivalent to a logical "NOT" of the current
    /// policy.
    /// </summary>
    /// <param name="policyResult">The policy to negate</param>
    /// <returns>A new <see cref="PolicyBase{TModel,TMetadata}" /> that will perform the "Not" operation when evaluated.</returns>
    public static PolicyResultBase<TMetadata> operator !(PolicyResultBase<TMetadata> policyResult) =>
        policyResult.Not();
}
