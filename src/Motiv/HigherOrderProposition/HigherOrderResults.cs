using Motiv.Traversal;

namespace Motiv.HigherOrderProposition;

/// <summary>
///     How a higher-order proposition reaches its decision over a sequence of models.
/// </summary>
internal static class HigherOrderResults
{
    /// <summary>
    ///     Materializes <paramref name="source" /> into an array of per-model results in a single pass, then
    ///     applies <paramref name="decide" /> to them — the whole of a higher-order proposition's decision, and
    ///     the only entry point to either half.
    /// </summary>
    /// <remarks>
    ///     <paramref name="project" /> receives the per-call <paramref name="state" /> (e.g. the underlying
    ///     predicate or resolver) as an explicit argument, so that call sites can pass a non-capturing
    ///     <c>static</c> lambda and keep the materialization free of per-evaluation closure allocations.
    ///     <para>
    ///     The whole decision runs inside <see cref="EvaluationBudget.Exclude" />: a collection of a quarter of
    ///     a million models is not a composition of a quarter of a million nodes, and
    ///     <see cref="MotivLimits.MaxEvaluationSize" /> has always excluded this seam. The scope spans the
    ///     enumeration as well as the projection, because producing an element is part of resolving it. Each
    ///     element is budgeted afresh, so a single element's own composition is still bounded.
    ///     </para>
    ///     <para>
    ///     <b>The predicate is inside the scope, and materializing without deciding is not offered.</b> A
    ///     predicate supplied through <c>As(...)</c> may evaluate a proposition of its own — a quorum read from
    ///     a threshold rule — and that evaluation is how the node reaches its answer rather than part of the
    ///     composition the node sits in. Charged, adding such a predicate could make the composition around it
    ///     refuse, at a node with nothing to do with the one that overspent
    ///     (<see href="https://github.com/karlssberg/Motiv/issues/208">#208</see>). Materialization and the
    ///     predicate are one method rather than two because each higher-order proposition writes its own
    ///     <c>EvaluateModels</c>: a seam a caller can half-use is a seam most callers get right and one gets
    ///     wrong, with nothing to say which.
    ///     </para>
    ///     <para>
    ///     <paramref name="decide" /> takes an <see cref="IEnumerable{T}" /> rather than the array, because that
    ///     is the shape of the predicate a caller supplies. So the decision itself does allocate an enumerator,
    ///     once — the allocation <see cref="Materialize{TSource,TState,TResult}" /> goes to some length to avoid
    ///     <em>per element</em>.
    ///     </para>
    /// </remarks>
    internal static (TResult[] Results, bool IsSatisfied) MaterializeAndDecide<TSource, TState, TResult>(
        IEnumerable<TSource> source,
        TState state,
        Func<TSource, TState, TResult> project,
        Func<IEnumerable<TResult>, bool> decide)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));

        // Excluded for the whole decision — enumeration, projection and predicate. See the remarks above.
        using var exclusion = EvaluationBudget.Exclude();

        var results = Materialize(source, state, project);
        return (results, decide(results));
    }

    /// <summary>
    ///     Projects every element in one pass. Arrays — the type the internal hot path always supplies — are
    ///     indexed directly to avoid per-element interface dispatch; other <see cref="IReadOnlyList{T}" />
    ///     sources (e.g. <see cref="List{T}" />) are pre-sized and filled via an indexed loop; anything else
    ///     buffers. Either way this avoids the LINQ iterator, buffer-doubling and boxed-enumerator allocations
    ///     of <c>models.Select(...).ToArray()</c> on the higher-order evaluation hot path.
    /// </summary>
    private static TResult[] Materialize<TSource, TState, TResult>(
        IEnumerable<TSource> source,
        TState state,
        Func<TSource, TState, TResult> project)
    {
        if (source is TSource[] array)
        {
            var results = new TResult[array.Length];
            for (var i = 0; i < array.Length; i++)
                results[i] = project(array[i], state);

            return results;
        }

        if (source is IReadOnlyList<TSource> list)
        {
            var count = list.Count;
            var results = new TResult[count];
            for (var i = 0; i < count; i++)
                results[i] = project(list[i], state);

            return results;
        }

        var buffer = new List<TResult>();
        foreach (var item in source)
            buffer.Add(project(item, state));

        return buffer.ToArray();
    }
}
