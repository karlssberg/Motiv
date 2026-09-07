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
    ///     is the shape of the predicate a caller supplies. The results reach it as the array, so how often it is
    ///     enumerated — and therefore how many enumerators are allocated — is the predicate's business: none for
    ///     one that ignores its argument, one for <c>All(...)</c>, two for a <c>Count(...)</c> and an
    ///     <c>Any(...)</c>. What the loop below buys is that none of that is <em>per element</em>.
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
    ///     Resolves a result's causes through the caller's cause selector, outside the budget of whatever
    ///     evaluation happens to be reading the property.
    /// </summary>
    /// <remarks>
    ///     <b>The selector runs after <c>Satisfied</c> is fixed.</b> Every higher-order result takes its
    ///     outcome as a constructor argument, so nothing resolved from the result afterwards can change what
    ///     it decided — the selector picks which elements to <em>name</em> as the cause of a decision already
    ///     made. That is describing a decision rather than reaching one, which is the argument that keeps
    ///     telemetry's explanation rendering excluded
    ///     (<see href="https://github.com/karlssberg/Motiv/issues/209">#209</see>) and the argument that keeps
    ///     a leaf predicate counted, because a leaf predicate <em>is</em> how its node reaches an answer.
    ///     <para>
    ///     Charged, it was charged to whichever evaluation was in flight at the moment of the first read — and
    ///     because the property is memoized, that is a fact about who read it first rather than about the
    ///     composition. The same rule was therefore accepted or refused depending on whether an earlier
    ///     consumer, Motiv's own telemetry among them, had already touched the property
    ///     (<see href="https://github.com/karlssberg/Motiv/issues/213">#213</see>).
    ///     </para>
    ///     <para>
    ///     The default selector for <c>As(...)</c> is <c>Causes.Get(..., higherOrderPredicate)</c>, which
    ///     re-invokes the predicate <see cref="MaterializeAndDecide" /> already excludes, so leaving this one
    ///     charged had a single delegate excluded in one place and counted in another.
    ///     </para>
    /// </remarks>
    internal static TElement[] ResolveCauses<TElement>(
        bool satisfied,
        TElement[] underlyingResults,
        Func<bool, IEnumerable<TElement>, IEnumerable<TElement>> causeSelector)
    {
        using var exclusion = EvaluationBudget.Exclude();

        return causeSelector(satisfied, underlyingResults).ToArray();
    }

    /// <summary>
    ///     Resolves a result's single <c>WhenTrue</c>/<c>WhenFalse</c> value outside the reader's budget. See
    ///     <see cref="ResolveCauses{TElement}" /> for why.
    /// </summary>
    /// <remarks>
    ///     <b>A sequence-returning delegate belongs in <see cref="ResolveValues{TEvaluation,TValue}" />, which
    ///     this will not tell you.</b> Passed one, <typeparamref name="TValue" /> binds to the sequence and the
    ///     scope closes over the invocation alone — leaving the caller's code to run at enumeration, outside
    ///     it, which is the state this seam exists to end.
    /// </remarks>
    internal static TValue ResolveValue<TEvaluation, TValue>(
        bool satisfied,
        TEvaluation evaluation,
        Func<TEvaluation, TValue> whenTrue,
        Func<TEvaluation, TValue> whenFalse)
    {
        using var exclusion = EvaluationBudget.Exclude();

        return satisfied ? whenTrue(evaluation) : whenFalse(evaluation);
    }

    /// <summary>
    ///     Resolves a result's yielded <c>WhenTrue</c>/<c>WhenFalse</c> values outside the reader's budget,
    ///     <em>materializing them inside the scope</em>: a yielding delegate is an iterator block, so invoking
    ///     it runs none of the caller's code and enumerating it runs all of it.
    /// </summary>
    /// <remarks>
    ///     <b>The return is nullable because a null sequence is a contract, not an accident.</b> The three
    ///     assertion-resolving results — the <c>MultiAssertionExplanation</c> families — degrade gracefully
    ///     when a caller's resolver returns null: <c>Values</c> comes back empty and <c>Explanation</c>
    ///     falls back to the statement's own reason, which
    ///     <c>HigherOrderFrom*MultiAssertionExplanationBooleanResultTests</c> pin in both outcomes. They
    ///     reached that through <c>?.ToArray()</c>; the four metadata-resolving results reached a prompt
    ///     <see cref="ArgumentNullException" /> through a bare <c>.ToArray()</c>, untested either way.
    ///     <para>
    ///     One seam has to stand for both, and it stands for the tested one — so the metadata families now
    ///     degrade where they used to throw. Declaring the return <c>TValue[]?</c> rather than suppressing
    ///     with <c>!</c> is what keeps that legible: the null is real, it reaches the caller, and the
    ///     suppression belongs at the field whose non-nullable declaration is the thing being asserted.
    ///     </para>
    /// </remarks>
    internal static TValue[]? ResolveValues<TEvaluation, TValue>(
        bool satisfied,
        TEvaluation evaluation,
        Func<TEvaluation, IEnumerable<TValue>> whenTrue,
        Func<TEvaluation, IEnumerable<TValue>> whenFalse)
    {
        using var exclusion = EvaluationBudget.Exclude();

        return (satisfied ? whenTrue(evaluation) : whenFalse(evaluation))?.ToArray();
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
