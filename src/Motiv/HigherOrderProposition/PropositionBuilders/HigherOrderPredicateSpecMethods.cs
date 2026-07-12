using Converj.Attributes;

namespace Motiv.HigherOrderProposition.PropositionBuilders;

internal static class HigherOrderPredicateSpecMethods
{
    [FluentMethodTemplate]
    internal static HigherOrderSpecPredicateOperation<TModel, TUnderlyingMetadata> As<TModel, TUnderlyingMetadata>(
        Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
        Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector)
    {
        return new HigherOrderSpecPredicateOperation<TModel, TUnderlyingMetadata>(
            higherOrderPredicate,
            causeSelector);
    }

    [FluentMethodTemplate]
    internal static HigherOrderSpecPredicateOperation<TModel, TUnderlyingMetadata> As<TModel, TUnderlyingMetadata>(
        Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate)
    {
        return new HigherOrderSpecPredicateOperation<TModel, TUnderlyingMetadata>(
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
    internal static HigherOrderSpecPredicateOperation<TModel, TUnderlyingMetadata> AsAllSatisfied<TModel, TUnderlyingMetadata>()
    {
        return new HigherOrderSpecPredicateOperation<TModel, TUnderlyingMetadata>(
            booleanResults => booleanResults.AllTrue(),
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
    internal static HigherOrderSpecPredicateOperation<TModel, TUnderlyingMetadata> AsAnySatisfied<TModel, TUnderlyingMetadata>()
    {
        return new HigherOrderSpecPredicateOperation<TModel, TUnderlyingMetadata>(
            booleanResults => booleanResults.AnyTrue(),
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
    internal static HigherOrderSpecPredicateOperation<TModel, TUnderlyingMetadata> AsAtLeastNSatisfied<TModel, TUnderlyingMetadata>(int n)
    {
        n.ThrowIfLessThan(0, nameof(n));

        return new HigherOrderSpecPredicateOperation<TModel, TUnderlyingMetadata>(
            HigherOrderPredicate,
            (_, results) => Causes.SatisfiedElseAll(results),
            HigherOrderShortCircuit.AtLeast(n));

        bool HigherOrderPredicate(IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> booleanResults) =>
            booleanResults.CountTrue() >= n;    }

    /// <summary>
    /// Creates a higher order proposition which is satisfied if at most 'n' of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="n">The maximum number of underlying propositions that can be satisfied.</param>
    /// <returns>The next build step.</returns>
    [FluentMethodTemplate]
    internal static HigherOrderSpecPredicateOperation<TModel, TUnderlyingMetadata> AsAtMostNSatisfied<TModel, TUnderlyingMetadata>(int n)
    {
        n.ThrowIfLessThan(0, nameof(n));

        return new HigherOrderSpecPredicateOperation<TModel, TUnderlyingMetadata>(
            HigherOrderPredicate,
            (_, results) => Causes.SatisfiedElseAll(results),
            HigherOrderShortCircuit.AtMost(n));

        bool HigherOrderPredicate(IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> booleanResults) =>
            booleanResults.CountTrue() <= n;    }

    /// <summary>
    /// Creates a higher order proposition that is satisfied if none of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <returns>The next build step.</returns>
    [FluentMethodTemplate]
    internal static HigherOrderSpecPredicateOperation<TModel, TUnderlyingMetadata> AsNoneSatisfied<TModel, TUnderlyingMetadata>()
    {
        return new HigherOrderSpecPredicateOperation<TModel, TUnderlyingMetadata>(
            booleanResults => booleanResults.AllFalse(),
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
    internal static HigherOrderSpecPredicateOperation<TModel, TUnderlyingMetadata> AsNSatisfied<TModel, TUnderlyingMetadata>(int n)
    {
        n.ThrowIfLessThan(0, nameof(n));

        return new HigherOrderSpecPredicateOperation<TModel, TUnderlyingMetadata>(
            HigherOrderPredicate,
            (_, results) => Causes.SatisfiedElseAll(results),
            HigherOrderShortCircuit.Exactly(n));

        bool HigherOrderPredicate(IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> booleanResults) =>
            booleanResults.CountTrue() == n;    }
}
