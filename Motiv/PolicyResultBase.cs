using Motiv.Not;

namespace Motiv;

/// <summary>
/// Represents a "policy result" whereby an arbitrary rule causes a single metadata instance to be returned.
/// </summary>
/// <typeparam name="TMetadata"></typeparam>
public abstract class PolicyResultBase<TMetadata> : BooleanResultBase<TMetadata>
{
    /// <summary>
    /// The single metadata instance that is returned by the policy.
    /// </summary>
    public abstract TMetadata Value { get; }

    /// <summary>
    /// Returns a new instance of <see cref="NotPolicyResult{TMetadata}" /> that represents the logical negation of
    /// the current instance.
    /// </summary>
    /// <returns>
    /// A new instance of <see cref="NotBooleanOperationResult{TMetadata}" /> that represents the logical negation of the
    /// current instance.
    /// </returns>
    public new PolicyResultBase<TMetadata> Not() => new NotPolicyResult<TMetadata>(this);
}
