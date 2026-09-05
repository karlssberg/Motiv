using Motiv.Traversal;

namespace Motiv.HigherOrderProposition;

/// <summary>
///     Materializes a sequence of models into an array of per-model results in a single pass. Arrays (the type the
///     internal hot path always supplies) are indexed directly to avoid per-element interface dispatch; other
///     <see cref="IReadOnlyList{T}" /> sources (e.g. <see cref="List{T}" />) are pre-sized and filled via an indexed
///     loop. Either way this avoids the LINQ iterator, buffer-doubling, and boxed-enumerator allocations of
///     <c>models.Select(...).ToArray()</c> on the higher-order evaluation hot path.
/// </summary>
/// <remarks>
///     The projection receives the per-call <paramref name="state" /> (e.g. the underlying predicate or resolver)
///     as an explicit argument so that call sites can pass a non-capturing <c>static</c> lambda, keeping the
///     materialization free of per-evaluation closure allocations.
///     <para>
///     Each element is resolved through <see cref="EvaluationBudget.OutsideBudget{TArgument,TState,TResult}" />: a
///     collection of a quarter of a million models is not a composition of a quarter of a million nodes, and
///     <see cref="MotivLimits.MaxEvaluationSize" /> has always excluded this seam. Each element is budgeted
///     afresh, so a single element's own composition is still bounded.
///     </para>
/// </remarks>
internal static class HigherOrderResults
{
    internal static TResult[] Materialize<TSource, TState, TResult>(
        IEnumerable<TSource> source,
        TState state,
        Func<TSource, TState, TResult> project)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));

        if (source is TSource[] array)
        {
            var results = new TResult[array.Length];
            for (var i = 0; i < array.Length; i++)
                results[i] = EvaluationBudget.OutsideBudget(array[i], state, project);

            return results;
        }

        if (source is IReadOnlyList<TSource> list)
        {
            var count = list.Count;
            var results = new TResult[count];
            for (var i = 0; i < count; i++)
                results[i] = EvaluationBudget.OutsideBudget(list[i], state, project);

            return results;
        }

        var buffer = new List<TResult>();
        foreach (var item in source)
            buffer.Add(EvaluationBudget.OutsideBudget(item, state, project));

        return buffer.ToArray();
    }
}
