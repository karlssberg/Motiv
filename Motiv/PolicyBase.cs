using Motiv.Not;
using Motiv.OrElse;

namespace Motiv;

/// <summary>
/// Represents a "policy" whereby an arbitrary rule causes a single metadata instance to be returned for both the
/// true and false conditions.
/// </summary>
/// <typeparam name="TModel">The model type that the policy is for.</typeparam>
/// <typeparam name="TMetadata">The metadata type that the policy returns.</typeparam>
public abstract class PolicyBase<TModel, TMetadata> : SpecBase<TModel, TMetadata>
{
    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(TModel model) => IsPolicySatisfiedBy(model);

    /// <summary>
    /// Executes the proposition as a policy and returns a <see cref="PolicyResultBase{TMetadata}" /> primarily
    /// containing a single metadata instance.
    /// </summary>
    /// <param name="model">The model to evaluate</param>
    /// <returns>A <see cref="PolicyResultBase{TMetadata}" /> containing the metadata instance and the boolean result.</returns>
    public new PolicyResultBase<TMetadata> IsSatisfiedBy(TModel model) => IsPolicySatisfiedBy(model);

    /// <summary>
    /// Executes the policy as a policy and returns a <see cref="PolicyResultBase{TMetadata}" /> primarily
    /// containing a single metadata instance.
    /// </summary>
    /// <param name="model">The model to evaluate</param>
    /// <returns>A <see cref="PolicyResultBase{TMetadata}" /> containing the metadata instance and the boolean result.</returns>
    protected abstract PolicyResultBase<TMetadata> IsPolicySatisfiedBy(TModel model);

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

    /// <summary>Creates a new policy that is equivalent to a logical "NOT" of the current policy.</summary>
    /// <returns>A new <see cref="PolicyBase{TModel,TMetadata}" /> that will perform the "Not" operation when evaluated.</returns>
    public new PolicyBase<TModel, TMetadata> Not() => new NotPolicy<TModel, TMetadata>(this);

    /// <summary>Creates a new policy that is equivalent to a logical "NOT" of the current policy.</summary>
    /// <param name="policy">The policy to negate</param>
    /// <returns>A new <see cref="PolicyBase{TModel,TMetadata}" /> that will perform the "Not" operation when evaluated.</returns>
    public static PolicyBase<TModel, TMetadata> operator !(PolicyBase<TModel, TMetadata> policy) => policy.Not();
}
