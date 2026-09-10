namespace Motiv.Traversal;

/// <summary>
/// The asynchronous counterpart of <see cref="EvaluationFold" />. Same frame machine, awaiting each
/// operand rather than calling it.
/// </summary>
/// <remarks>
/// Three things differ from the synchronous driver, all forced by <c>async</c>:
/// <list type="bullet">
/// <item>Frames are addressed by index rather than through a <c>ref</c> local, because an async method
/// cannot hold a by-ref local across an <c>await</c>. Array element access is still a variable, so the
/// frames are mutated in place either way.</item>
/// <item>There is no per-thread frame buffer. A continuation may resume on a different thread than the
/// one that started the fold, so a thread-static buffer would be returned to the wrong thread — and an
/// async evaluation already allocates a state machine per operand it awaits, next to which one array is
/// not the cost worth chasing.</item>
/// <item>The budget is flowed rather than thread-static, for the same reason and a sharper one: the
/// slot a continuation resumes onto may hold a <em>suspended</em> evaluation's count, so a thread-static
/// would not merely be unavailable but would let two interleaved evaluations corrupt each other. See
/// <see cref="EvaluationBudget" />, and
/// <see href="https://github.com/karlssberg/Motiv/issues/204">#204</see> for why this arrived a change
/// later than the synchronous half.</item>
/// <item>A concurrent operation is folded by a second loop rather than the frame machine, because its
/// operands are not ordered with respect to each other. See
/// <see cref="FoldConcurrentlyAsync{TModel,TMetadata,TValue,TDriver}" /> and
/// <see href="https://github.com/karlssberg/Motiv/issues/145">#145</see>.</item>
/// </list>
/// </remarks>
internal static class AsyncEvaluationFold
{
    private const int InitialCapacity = 8;

    /// <summary>Evaluates <paramref name="root" />, producing the composed result.</summary>
    internal static ValueTask<BooleanResultBase<TMetadata>> EvaluateAsync<TModel, TMetadata>(
        IAsyncFoldableOperation<TModel, TMetadata> root,
        TModel model,
        CancellationToken cancellationToken) =>
        FoldAsync<TModel, TMetadata, BooleanResultBase<TMetadata>, ResultDriver<TModel, TMetadata>>(
            root, model, cancellationToken);

    /// <summary>
    /// Evaluates <paramref name="root" />, producing the composed policy result. The cast is safe for the
    /// reason <see cref="EvaluationFold.EvaluatePolicy{TModel,TMetadata}" /> gives.
    /// </summary>
    internal static async ValueTask<PolicyResultBase<TMetadata>> EvaluatePolicyAsync<TModel, TMetadata>(
        IAsyncFoldableOperation<TModel, TMetadata> root,
        TModel model,
        CancellationToken cancellationToken) =>
        (PolicyResultBase<TMetadata>)await EvaluateAsync(root, model, cancellationToken).ConfigureAwait(false);

    /// <summary>Evaluates <paramref name="root" /> for its outcome alone, composing no results.</summary>
    internal static ValueTask<bool> MatchesAsync<TModel, TMetadata>(
        IAsyncFoldableOperation<TModel, TMetadata> root,
        TModel model,
        CancellationToken cancellationToken) =>
        FoldAsync<TModel, TMetadata, bool, MatchDriver<TModel, TMetadata>>(root, model, cancellationToken);

    private static async ValueTask<TValue> FoldAsync<TModel, TMetadata, TValue, TDriver>(
        IAsyncFoldableOperation<TModel, TMetadata> root,
        TModel model,
        CancellationToken cancellationToken)
        where TDriver : struct, IAsyncFoldDriver<TModel, TMetadata, TValue>
    {
        var driver = default(TDriver);

        // Claimed in the synchronous prefix, before the first await, so that the counter is in the
        // execution context every continuation below captures. See EvaluationBudget.EnterAsync.
        using var budget = EvaluationBudget.EnterAsync();

        if (root.IsConcurrent)
            return await FoldConcurrentlyAsync<TModel, TMetadata, TValue, TDriver>(
                    root, model, budget, cancellationToken)
                .ConfigureAwait(false);

        var frames = new Frame<TModel, TMetadata, TValue>[InitialCapacity];
        frames[0] = new Frame<TModel, TMetadata, TValue>(root);
        var depth = 1;

        TValue completed = default!;
        var hasCompleted = false;

        while (true)
        {
            var index = depth - 1;

            if (hasCompleted)
            {
                frames[index].Accept(completed);
                hasCompleted = false;
            }

            var next = frames[index].NextOperand(driver);

            if (next is null)
            {
                var value = driver.Combine(
                    frames[index].Operation,
                    frames[index].First,
                    frames[index].Second,
                    frames[index].HasSecond);

                if (--depth == 0)
                    return value;

                completed = value;
                hasCompleted = true;
                continue;
            }

            budget.Charge();

            if (next is IAsyncFoldableOperation<TModel, TMetadata> operation)
            {
                if (operation.IsConcurrent)
                {
                    completed = await FoldConcurrentlyAsync<TModel, TMetadata, TValue, TDriver>(
                            operation, model, budget, cancellationToken)
                        .ConfigureAwait(false);
                    hasCompleted = true;
                    continue;
                }

                if (depth == frames.Length)
                    Array.Resize(ref frames, depth * 2);

                frames[depth++] = new Frame<TModel, TMetadata, TValue>(operation);
                continue;
            }

            completed = await driver.LeafAsync(next, model, cancellationToken).ConfigureAwait(false);
            hasCompleted = true;
        }
    }

    /// <summary>
    /// Folds a <em>region</em> of concurrent operations — <paramref name="root" /> and every concurrent
    /// operation reachable from it through an unbroken run of them — as one fan-out: every operand at the
    /// region's boundary is started at once, and the region is composed from their answers afterwards.
    /// </summary>
    /// <remarks>
    /// <b>The nest already meant "start every one of these at once", and the nesting was only how that
    /// was spelled.</b> <c>a.AndConcurrently(b.AndConcurrently(c))</c> starts <c>a</c> and the inner node
    /// together, and the inner node starts <c>b</c> and <c>c</c> the moment it is reached, so all three
    /// are in flight either way. Flattening the region therefore preserves the concurrency exactly rather
    /// than approximating it — what it drops is the <c>Task.WhenAll</c> per layer, and with it the stack
    /// frame per layer that put a nest's ceiling at a few hundred
    /// (<see href="https://github.com/karlssberg/Motiv/issues/145">#145</see>).
    /// <para>
    /// This is a second loop rather than a case in the frame machine because the two disagree about
    /// exactly one thing: a frame's operands are ordered — the second is asked for only once the first
    /// has answered, which is what makes short-circuiting expressible — and a concurrent operation's are
    /// not. What lets the region be walked without evaluating anything is that concurrency implies
    /// eagerness: <c>NextOperand</c> returns the same operand whichever outcome it is told, so the
    /// region's shape is a property of the composition rather than of the run.
    /// </para>
    /// <para>
    /// The walk is breadth-first by construction — appending to a list being indexed forward — so it
    /// costs no stack of its own, and every node's operands sit at a higher index than the node, which is
    /// what lets the composing pass run backwards over one array.
    /// </para>
    /// <para>
    /// <b>What is charged, and why it is the region rather than its boundary.</b> Nothing walked these
    /// nodes before, so nothing charged them: a fan-out reached from a fold was charged by that fold as
    /// an operand and its nested siblings by no one, which made a nest of any depth cost the budget one
    /// node. That was invisible while the stack capped the depth at a few hundred, and is not now. So the
    /// region charges each concurrent node it absorbs. It does not charge an operand it hands to a fold
    /// of its own, which charges its root on entry — charging here as well would count it twice.
    /// </para>
    /// </remarks>
    private static async ValueTask<TValue> FoldConcurrentlyAsync<TModel, TMetadata, TValue, TDriver>(
        IAsyncFoldableOperation<TModel, TMetadata> root,
        TModel model,
        EvaluationBudget.Ownership budget,
        CancellationToken cancellationToken)
        where TDriver : struct, IAsyncFoldDriver<TModel, TMetadata, TValue>
    {
        var driver = default(TDriver);

        var region = new List<IAsyncFoldableOperation<TModel, TMetadata>> { root };
        var placements = new List<(int First, int Second)>();
        var boundary = new List<Task<TValue>>();

        for (var node = 0; node < region.Count; node++)
        {
            var operation = region[node];
            // Both operands, unconditionally: a concurrent operation is binary and eager, which is
            // the contract IAsyncFoldableOperation.IsConcurrent states and this walk depends on.
            var first = Place(operation.FirstOperand);
            var second = Place(
                operation.NextOperand(firstSatisfied: true) ?? ThrowSecondOperandMissing(operation));
            placements.Add((first, second));
        }

        var boundaryValues = await Task.WhenAll(boundary).ConfigureAwait(false);

        var composed = new TValue[region.Count];
        for (var node = region.Count - 1; node >= 0; node--)
        {
            var (first, second) = placements[node];
            composed[node] = driver.Combine(
                region[node],
                Value(first),
                Value(second),
                hasSecond: true);
        }

        return composed[0];

        // A concurrent operand is absorbed into the region and answered by this fold; any other is
        // started now and answered by the task it returns. The two are told apart by the sign of the
        // placement: a region index is non-negative, a boundary index is its bitwise complement.
        int Place(AsyncSpecBase<TModel, TMetadata> operand)
        {
            switch (operand)
            {
                case IAsyncFoldableOperation<TModel, TMetadata> { IsConcurrent: true } nested:
                    budget.Charge();
                    region.Add(nested);
                    return region.Count - 1;

                case IAsyncFoldableOperation<TModel, TMetadata>:
                    break; // Charged by the fold it is about to enter, as that fold's root.

                default:
                    budget.Charge();
                    break;
            }

            boundary.Add(driver.LeafAsync(operand, model, cancellationToken).AsTask());
            return ~(boundary.Count - 1);
        }

        TValue Value(int placement) =>
            placement >= 0 ? composed[placement] : boundaryValues[~placement];
    }

    /// <summary>
    /// The refusal of a concurrent operation that breaks the contract the region walk rests on. Kept
    /// out of the walk so that a branch never taken does not weigh against inlining the loop it sits in.
    /// </summary>
    private static AsyncSpecBase<TModel, TMetadata> ThrowSecondOperandMissing<TModel, TMetadata>(
        IAsyncFoldableOperation<TModel, TMetadata> operation) =>
        throw new InvalidOperationException(
            $"{operation.GetType().Name} reports {nameof(IAsyncFoldableOperation<TModel, TMetadata>.IsConcurrent)} " +
            "but supplied no second operand. A concurrent operation is binary and eager: " +
            $"{nameof(IAsyncFoldableOperation<TModel, TMetadata>.NextOperand)} must return an operand " +
            "whichever outcome it is told.");

    private struct Frame<TModel, TMetadata, TValue>(IAsyncFoldableOperation<TModel, TMetadata> operation)
    {
        private bool _hasFirst;
        private bool _nextSettled;

        public IAsyncFoldableOperation<TModel, TMetadata> Operation { get; } = operation;

        public TValue First { get; private set; } = default!;

        public TValue Second { get; private set; } = default!;

        public bool HasSecond { get; private set; }

        public void Accept(TValue value)
        {
            if (_hasFirst)
            {
                Second = value;
                HasSecond = true;
                return;
            }

            First = value;
            _hasFirst = true;
        }

        public AsyncSpecBase<TModel, TMetadata>? NextOperand<TDriver>(TDriver driver)
            where TDriver : struct, IAsyncFoldDriver<TModel, TMetadata, TValue>
        {
            if (!_hasFirst)
                return Operation.FirstOperand;

            if (_nextSettled)
                return null;

            _nextSettled = true;
            return Operation.NextOperand(driver.Satisfied(First));
        }
    }

    private interface IAsyncFoldDriver<TModel, TMetadata, TValue>
    {
        ValueTask<TValue> LeafAsync(
            AsyncSpecBase<TModel, TMetadata> spec,
            TModel model,
            CancellationToken cancellationToken);

        bool Satisfied(TValue value);

        TValue Combine(
            IAsyncFoldableOperation<TModel, TMetadata> operation,
            TValue first,
            TValue second,
            bool hasSecond);
    }

    private readonly struct ResultDriver<TModel, TMetadata>
        : IAsyncFoldDriver<TModel, TMetadata, BooleanResultBase<TMetadata>>
    {
        public ValueTask<BooleanResultBase<TMetadata>> LeafAsync(
            AsyncSpecBase<TModel, TMetadata> spec,
            TModel model,
            CancellationToken cancellationToken) =>
            spec.EvaluateSpecAsyncInternal(model, cancellationToken);

        public bool Satisfied(BooleanResultBase<TMetadata> value) => value.Satisfied;

        public BooleanResultBase<TMetadata> Combine(
            IAsyncFoldableOperation<TModel, TMetadata> operation,
            BooleanResultBase<TMetadata> first,
            BooleanResultBase<TMetadata> second,
            bool hasSecond) =>
            operation.Combine(first, hasSecond ? second : null);
    }

    private readonly struct MatchDriver<TModel, TMetadata> : IAsyncFoldDriver<TModel, TMetadata, bool>
    {
        public ValueTask<bool> LeafAsync(
            AsyncSpecBase<TModel, TMetadata> spec,
            TModel model,
            CancellationToken cancellationToken) =>
            spec.MatchesAsync(model, cancellationToken);

        public bool Satisfied(bool value) => value;

        public bool Combine(
            IAsyncFoldableOperation<TModel, TMetadata> operation,
            bool first,
            bool second,
            bool hasSecond) =>
            operation.CombineMatches(first, hasSecond ? second : null);
    }
}
