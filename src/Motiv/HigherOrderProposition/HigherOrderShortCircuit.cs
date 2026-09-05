using Motiv.Traversal;

namespace Motiv.HigherOrderProposition;

internal enum HigherOrderOp
{
    All,
    Any,
    None,
    AtLeast,
    AtMost,
    Exactly
}

/// <summary>
/// Describes a built-in higher-order operation as data and evaluates it directly against a per-model boolean
/// projection with array/list fast-paths and per-operation short-circuit. This is the allocation-free,
/// boolean-only counterpart to the result-materializing higher-order predicate used by the full evaluation path.
/// </summary>
internal readonly struct HigherOrderShortCircuit(HigherOrderOp op, int n)
{
    internal static HigherOrderShortCircuit All { get; } = new(HigherOrderOp.All, 0);
    internal static HigherOrderShortCircuit Any { get; } = new(HigherOrderOp.Any, 0);
    internal static HigherOrderShortCircuit None { get; } = new(HigherOrderOp.None, 0);
    internal static HigherOrderShortCircuit AtLeast(int n) => new(HigherOrderOp.AtLeast, n);
    internal static HigherOrderShortCircuit AtMost(int n) => new(HigherOrderOp.AtMost, n);
    internal static HigherOrderShortCircuit Exactly(int n) => new(HigherOrderOp.Exactly, n);

    /// <summary>
    /// Evaluates the operation over <paramref name="source" />, deriving each model's boolean via
    /// <paramref name="project" /> (which receives the per-call <paramref name="state" /> so call sites can pass a
    /// non-capturing <c>static</c> lambda). Arrays and <see cref="IReadOnlyList{T}" /> sources iterate by index to
    /// avoid enumerator allocation; the operation's decision logic is shared across all iteration shapes.
    /// </summary>
    /// <remarks>
    /// Each element is derived through <see cref="EvaluationBudget.OutsideBudget{TArgument,TState,TResult}" />, for
    /// the reason <see cref="HigherOrderResults" /> gives — this is the allocation-free half of the same seam.
    /// </remarks>
    internal bool Evaluate<TModel, TState>(
        IEnumerable<TModel> source,
        TState state,
        Func<TModel, TState, bool> project)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));

        // "At least n" with n <= 0 is vacuously satisfied, so decide without evaluating any element.
        if (op == HigherOrderOp.AtLeast && n <= 0)
            return true;

        var trueCount = 0;

        switch (source)
        {
            case TModel[] array:
            {
                for (var i = 0; i < array.Length; i++)
                {
                    var satisfied = EvaluationBudget.OutsideBudget(array[i], state, project);
                    if (TryDecide(satisfied, ref trueCount, out var decided))
                        return decided;
                }

                break;
            }

            case IReadOnlyList<TModel> list:
            {
                var count = list.Count;
                for (var i = 0; i < count; i++)
                {
                    var satisfied = EvaluationBudget.OutsideBudget(list[i], state, project);
                    if (TryDecide(satisfied, ref trueCount, out var decided))
                        return decided;
                }

                break;
            }

            default:
            {
                foreach (var item in source)
                {
                    var satisfied = EvaluationBudget.OutsideBudget(item, state, project);
                    if (TryDecide(satisfied, ref trueCount, out var decided))
                        return decided;
                }

                break;
            }
        }

        return FinalDecision(trueCount);
    }

    // Applies the operation's early-exit rule to one projected boolean. Returns true (with `decided` set) when the
    // overall outcome is already determined; otherwise returns false to continue iterating.
    private bool TryDecide(bool satisfied, ref int trueCount, out bool decided)
    {
        if (satisfied)
            trueCount++;

        switch (op)
        {
            case HigherOrderOp.All:
                decided = false;
                return !satisfied;         // first false → false
            case HigherOrderOp.Any:
                decided = true;
                return satisfied;          // first true → true
            case HigherOrderOp.None:
                decided = false;
                return satisfied;          // first true → false
            case HigherOrderOp.AtLeast:
                decided = true;
                return trueCount >= n;      // reached n → true
            case HigherOrderOp.AtMost:
                decided = false;
                return trueCount > n;       // exceeded n → false
            case HigherOrderOp.Exactly:
                decided = false;
                return trueCount > n;       // exceeded n → false
            default:
                decided = false;
                return false;
        }
    }

    // Outcome when iteration completed without an early decision.
    private bool FinalDecision(int trueCount) =>
        op switch
        {
            HigherOrderOp.All => true,             // no false was seen
            HigherOrderOp.Any => false,            // no true was seen
            HigherOrderOp.None => true,            // no true was seen
            HigherOrderOp.AtLeast => trueCount >= n,
            HigherOrderOp.AtMost => true,          // never exceeded n
            HigherOrderOp.Exactly => trueCount == n,
            _ => false
        };
}
