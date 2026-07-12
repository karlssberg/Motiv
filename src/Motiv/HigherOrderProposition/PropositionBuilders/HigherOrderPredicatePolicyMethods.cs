using Converj.Attributes;

namespace Motiv.HigherOrderProposition.PropositionBuilders;

internal static class HigherOrderPredicatePolicyMethods
{
    [FluentMethodTemplate]
    internal static HigherOrderPolicyPredicateOperation<TModel, TUnderlyingMetadata> As<TModel, TUnderlyingMetadata>(
        Func<IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
        Func<bool, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>> causeSelector)
    {
        return new HigherOrderPolicyPredicateOperation<TModel, TUnderlyingMetadata>(
            higherOrderPredicate,
            causeSelector);
    }

    [FluentMethodTemplate]
    internal static HigherOrderPolicyPredicateOperation<TModel, TUnderlyingMetadata> As<TModel, TUnderlyingMetadata>(
        Func<IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate)
    {
        return new HigherOrderPolicyPredicateOperation<TModel, TUnderlyingMetadata>(
            higherOrderPredicate,
            (isSatisfied, results) => Causes.Get(isSatisfied, results, higherOrderPredicate));
    }

    /// <summary>
    /// Builds a proposition which is satisfied if all the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <returns>The next build step.</returns>
    [FluentMethodTemplate]
    internal static HigherOrderPolicyPredicateOperation<TModel, TUnderlyingMetadata> AsAllSatisfied<TModel, TUnderlyingMetadata>()
    {
        return new HigherOrderPolicyPredicateOperation<TModel, TUnderlyingMetadata>(
            policyResults => policyResults.AllTrue(),
            (isSatisfied, results) => isSatisfied ? results : results.WhereFalse(),
            HigherOrderShortCircuit.All);
    }

    /// <summary>
    /// Creates a higher order proposition that is satisfied if any of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <returns>The next build step.</returns>
    [FluentMethodTemplate]
    internal static HigherOrderPolicyPredicateOperation<TModel, TUnderlyingMetadata> AsAnySatisfied<TModel, TUnderlyingMetadata>()
    {
        return new HigherOrderPolicyPredicateOperation<TModel, TUnderlyingMetadata>(
            policyResults => policyResults.AnyTrue(),
            (isSatisfied, results) => isSatisfied ? results.WhereTrue() : results,
            HigherOrderShortCircuit.Any);
    }

    /// <summary>
    /// Creates a higher order proposition that is satisfied if at least 'n' of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="n">The minimum number of underlying propositions that need to be satisfied.</param>
    /// <returns>The next build step.</returns>
    [FluentMethodTemplate]
    internal static HigherOrderPolicyPredicateOperation<TModel, TUnderlyingMetadata> AsAtLeastNSatisfied<TModel, TUnderlyingMetadata>(int n)
    {
        n.ThrowIfLessThan(0, nameof(n));

        return new HigherOrderPolicyPredicateOperation<TModel, TUnderlyingMetadata>(
            HigherOrderPredicate,
            (_, results) => Causes.SatisfiedElseAll(results),
            HigherOrderShortCircuit.AtLeast(n));

        bool HigherOrderPredicate(IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>> policyResults) =>
            policyResults.CountTrue() >= n;    }

    /// <summary>
    /// Creates a higher order proposition which is satisfied if at most 'n' of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="n">The maximum number of underlying propositions that can be satisfied.</param>
    /// <returns>The next build step.</returns>
    [FluentMethodTemplate]
    internal static HigherOrderPolicyPredicateOperation<TModel, TUnderlyingMetadata> AsAtMostNSatisfied<TModel, TUnderlyingMetadata>(int n)
    {
        n.ThrowIfLessThan(0, nameof(n));

        return new HigherOrderPolicyPredicateOperation<TModel, TUnderlyingMetadata>(
            HigherOrderPredicate,
            (_, results) => Causes.SatisfiedElseAll(results),
            HigherOrderShortCircuit.AtMost(n));

        bool HigherOrderPredicate(IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>> policyResults) =>
            policyResults.CountTrue() <= n;    }

    /// <summary>
    /// Creates a higher order proposition that is satisfied if none of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <returns>The next build step.</returns>
    [FluentMethodTemplate]
    internal static HigherOrderPolicyPredicateOperation<TModel, TUnderlyingMetadata> AsNoneSatisfied<TModel, TUnderlyingMetadata>()
    {
        return new HigherOrderPolicyPredicateOperation<TModel, TUnderlyingMetadata>(
            policyResults => policyResults.AllFalse(),
            (isSatisfied, results) => isSatisfied ? results : results.WhereTrue(),
            HigherOrderShortCircuit.None);
    }

    /// <summary>
    /// Creates a higher order proposition that is satisfied if exactly 'n' of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="n">The exact number of underlying propositions that need to be satisfied.</param>
    /// <returns>A higher order proposition builder.</returns>
    [FluentMethodTemplate]
    internal static HigherOrderPolicyPredicateOperation<TModel, TUnderlyingMetadata> AsNSatisfied<TModel, TUnderlyingMetadata>(int n)
    {
        n.ThrowIfLessThan(0, nameof(n));

        return new HigherOrderPolicyPredicateOperation<TModel, TUnderlyingMetadata>(
            HigherOrderPredicate,
            (_, results) => Causes.SatisfiedElseAll(results),
            HigherOrderShortCircuit.Exactly(n));

        bool HigherOrderPredicate(IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>> policyResults) =>
            policyResults.CountTrue() == n;    }
}
