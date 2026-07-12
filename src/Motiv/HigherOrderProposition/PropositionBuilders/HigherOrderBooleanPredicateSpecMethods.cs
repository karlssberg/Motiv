using Converj.Attributes;
using Motiv.HigherOrderProposition;

namespace Motiv.HigherOrderProposition.PropositionBuilders;

internal static class HigherOrderBooleanPredicateSpecMethods
{
    [FluentMethodTemplate]
    internal static HigherOrderSpecBooleanPredicateOperation<TModel> As<TModel>(
        Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate,
        Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector)
    {
        return new HigherOrderSpecBooleanPredicateOperation<TModel>(
            higherOrderPredicate,
            causeSelector);
    }

    [FluentMethodTemplate]
    internal static HigherOrderSpecBooleanPredicateOperation<TModel> As<TModel>(
        Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate)
    {
        return new HigherOrderSpecBooleanPredicateOperation<TModel>(
            higherOrderPredicate,
            (isSatisfied, results) => Causes.Get(isSatisfied, results, higherOrderPredicate));
    }

    /// <summary>
    /// Builds a proposition which is satisfied if all the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <returns>The next build step.</returns>
    [FluentMethodTemplate]
    internal static HigherOrderSpecBooleanPredicateOperation<TModel> AsAllSatisfied<TModel>()
    {
        return new HigherOrderSpecBooleanPredicateOperation<TModel>(
            modelResult => modelResult.AllTrue(),
            (isSatisfied, results) => isSatisfied ? results : results.WhereFalse(),
            HigherOrderShortCircuit.All);
    }

    /// <summary>
    /// Creates a higher order proposition that is satisfied if any of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <returns>The next build step.</returns>
    [FluentMethodTemplate]
    internal static HigherOrderSpecBooleanPredicateOperation<TModel> AsAnySatisfied<TModel>()
    {
        return new HigherOrderSpecBooleanPredicateOperation<TModel>(
            modelResults => modelResults.AnyTrue(),
            (isSatisfied, results) => isSatisfied ? results.WhereTrue() : results,
            HigherOrderShortCircuit.Any);
    }

    /// <summary>
    /// Creates a higher order proposition that is satisfied if at least 'n' of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <param name="n">The minimum number of underlying propositions that need to be satisfied.</param>
    /// <returns>The next build step.</returns>
    [FluentMethodTemplate]
    internal static HigherOrderSpecBooleanPredicateOperation<TModel> AsAtLeastNSatisfied<TModel>(int n)
    {
        n.ThrowIfLessThan(0, nameof(n));

        return new HigherOrderSpecBooleanPredicateOperation<TModel>(
            HigherOrderPredicate,
            (_, results) => Causes.SatisfiedElseAll(results),
            HigherOrderShortCircuit.AtLeast(n));

        bool HigherOrderPredicate(IEnumerable<ModelResult<TModel>> modelResults) =>
            modelResults.CountTrue() >= n;    }

    /// <summary>
    /// Creates a higher order proposition which is satisfied if at most 'n' of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <param name="n">The maximum number of underlying propositions that can be satisfied.</param>
    /// <returns>The next build step.</returns>
    [FluentMethodTemplate]
    internal static HigherOrderSpecBooleanPredicateOperation<TModel> AsAtMostNSatisfied<TModel>(int n)
    {
        n.ThrowIfLessThan(0, nameof(n));

        return new HigherOrderSpecBooleanPredicateOperation<TModel>(
            HigherOrderPredicate,
            (_, results) => Causes.SatisfiedElseAll(results),
            HigherOrderShortCircuit.AtMost(n));

        bool HigherOrderPredicate(IEnumerable<ModelResult<TModel>> modelResults) =>
            modelResults.CountTrue() <= n;    }

    /// <summary>
    /// Creates a higher order proposition that is satisfied if none of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <returns>The next build step.</returns>
    [FluentMethodTemplate]
    internal static HigherOrderSpecBooleanPredicateOperation<TModel> AsNoneSatisfied<TModel>()
    {
        return new HigherOrderSpecBooleanPredicateOperation<TModel>(
            modelResults => modelResults.AllFalse(),
            (isSatisfied, results) => isSatisfied ? results : results.WhereTrue(),
            HigherOrderShortCircuit.None);
    }

    /// <summary>
    /// Creates a higher order proposition that is satisfied if exactly 'n' of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <param name="n">The exact number of underlying propositions that need to be satisfied.</param>
    /// <returns>A higher order proposition builder.</returns>
    [FluentMethodTemplate]
    internal static HigherOrderSpecBooleanPredicateOperation<TModel> AsNSatisfied<TModel>(int n)
    {
        n.ThrowIfLessThan(0, nameof(n));

        return new HigherOrderSpecBooleanPredicateOperation<TModel>(
            HigherOrderPredicate,
            (_, results) => Causes.SatisfiedElseAll(results),
            HigherOrderShortCircuit.Exactly(n));

        bool HigherOrderPredicate(IEnumerable<ModelResult<TModel>> modelResults) =>
            modelResults.CountTrue() == n;    }
}
