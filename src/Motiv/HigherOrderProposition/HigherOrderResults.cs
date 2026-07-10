namespace Motiv.HigherOrderProposition;

/// <summary>
///     Materializes a sequence of models into an array of per-model results in a single pass. When the source is
///     an <see cref="IReadOnlyList{T}" /> (arrays, <see cref="List{T}" />) the result array is pre-sized and filled
///     via an indexed loop, avoiding the LINQ iterator, buffer-doubling, and boxed-enumerator allocations of
///     <c>models.Select(...).ToArray()</c> on the higher-order evaluation hot path.
/// </summary>
/// <remarks>
///     The projection receives the per-call <paramref name="state" /> (e.g. the underlying predicate or resolver)
///     as an explicit argument so that call sites can pass a non-capturing <c>static</c> lambda, keeping the
///     materialization free of per-evaluation closure allocations.
/// </remarks>
internal static class HigherOrderResults
{
    internal static TResult[] Materialize<TSource, TState, TResult>(
        IEnumerable<TSource> source,
        TState state,
        Func<TSource, TState, TResult> project)
    {
        if (source is IReadOnlyList<TSource> list)
        {
            var results = new TResult[list.Count];
            for (var i = 0; i < list.Count; i++)
                results[i] = project(list[i], state);

            return results;
        }

        var buffer = new List<TResult>();
        foreach (var item in source)
            buffer.Add(project(item, state));

        return buffer.ToArray();
    }
}
