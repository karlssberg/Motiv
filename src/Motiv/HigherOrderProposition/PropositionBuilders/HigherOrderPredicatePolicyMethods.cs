using Motiv.FluentFactory.Generator;

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
        Func<IEnumerable<PolicyResult<TModel,TUnderlyingMetadata>>,bool> higherOrderPredicate =
            policyResults => policyResults.AllTrue();

        return new HigherOrderPolicyPredicateOperation<TModel, TUnderlyingMetadata>(
            higherOrderPredicate,
            (isSatisfied, results) => Causes.Get(isSatisfied, results, higherOrderPredicate));
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
        Func<IEnumerable<PolicyResult<TModel,TUnderlyingMetadata>>,bool> higherOrderPredicate =
            policyResults => policyResults.AnyTrue();

        return new HigherOrderPolicyPredicateOperation<TModel, TUnderlyingMetadata>(
            higherOrderPredicate,
            (isSatisfied, results) => Causes.Get(isSatisfied, results, higherOrderPredicate));
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
            CauseSelector);

        bool HigherOrderPredicate(IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>> policyResults) =>
            policyResults.CountTrue() >= n;

        IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>> CauseSelector(bool _, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>> policyResults)
        {
            var policyResultsArray = policyResults.ToArray();
            return policyResultsArray
                .WhereTrue()
                .ElseIfEmpty(policyResultsArray);
        }
    }

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
            CauseSelector);

        bool HigherOrderPredicate(IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>> policyResults) =>
            policyResults.CountTrue() <= n;

        IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>> CauseSelector(bool _, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>> policyResults)
        {
            var policyResultsArray = policyResults.ToArray();
            return policyResultsArray
                .WhereTrue()
                .ElseIfEmpty(policyResultsArray);
        }
    }

    /// <summary>
    /// Creates a higher order proposition that is satisfied if none of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <returns>The next build step.</returns>
    [FluentMethodTemplate]
    internal static HigherOrderPolicyPredicateOperation<TModel, TUnderlyingMetadata> AsNoneSatisfied<TModel, TUnderlyingMetadata>()
    {
        Func<IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate =
            policyResults => policyResults.AllFalse();

        return new HigherOrderPolicyPredicateOperation<TModel, TUnderlyingMetadata>(
            higherOrderPredicate,
            (isSatisfied, results) => Causes.Get(isSatisfied, results, higherOrderPredicate));
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
            CauseSelector);

        bool HigherOrderPredicate(IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>> policyResults) =>
            policyResults.CountTrue() == n;

        IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>> CauseSelector(bool _, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>> policyResults)
        {
            var policyResultsArray = policyResults.ToArray();
            return policyResultsArray
                .WhereTrue()
                .ElseIfEmpty(policyResultsArray);
        }
    }
}
