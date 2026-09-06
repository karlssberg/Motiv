namespace Motiv.Traversal;

/// <summary>
/// An iterative fold over a tree of <see cref="IOperationFold{TModel,TMetadata}" /> operations, used in
/// place of the non-tail recursion that evaluating a composition would otherwise use. The frames live on
/// the heap, so the depth a caller can compose is bounded by memory rather than by the thread's stack.
/// </summary>
/// <remarks>
/// The driver descends through operands that are themselves operations and evaluates everything else —
/// decorators, higher-order propositions, model-type changes, leaves — through the operand's own
/// evaluation. A chain of combinators is therefore flat at any depth; a chain of alternating decorators
/// still costs a frame per decorator layer.
/// <para>
/// Spec 3E left that standing on the argument that composition depth is attacker-controlled through a
/// rule document's operand array where decorator depth is not.
/// <see href="https://github.com/karlssberg/Motiv/issues/145">#145</see> measured the argument and
/// refuted its bound: a catalogue of propositions each referencing the one before it composes exactly
/// the alternating shape, whose ceiling is 1,046 links. The depth stays for now
/// (<see href="https://github.com/karlssberg/Motiv/issues/201">#201</see>), but the <em>size</em> no
/// longer resets with it — see <see cref="EvaluationBudget" />.
/// </para>
/// </remarks>
internal static class EvaluationFold
{
    /// <summary>
    /// Deep compositions grow this by doubling, so its only job is to keep the common shallow case from
    /// over-allocating.
    /// </summary>
    private const int InitialCapacity = 8;

    /// <summary>
    /// The largest buffer kept for reuse. A composition deep enough to grow past this is rare, and its
    /// frames are a rounding error next to the results it retains — where holding a two-megabyte array
    /// per thread for the rest of the process would not be.
    /// </summary>
    private const int MaxCachedCapacity = 64;

    /// <summary>Evaluates <paramref name="root" />, producing the composed result.</summary>
    internal static BooleanResultBase<TMetadata> Evaluate<TModel, TMetadata>(
        IOperationFold<TModel, TMetadata> root,
        TModel model) =>
        Fold<TModel, TMetadata, BooleanResultBase<TMetadata>, ResultDriver<TModel, TMetadata>>(root, model);

    /// <summary>
    /// Evaluates <paramref name="root" />, producing the composed policy result.
    /// </summary>
    /// <remarks>
    /// The cast is safe by construction and stated once here rather than at each policy operator: a
    /// policy operation's operands are policies, so every value the fold hands to
    /// <see cref="IOperationFold{TModel,TMetadata}.Combine" /> is a <see cref="PolicyResultBase{TMetadata}" />,
    /// and every policy operator composes one.
    /// </remarks>
    internal static PolicyResultBase<TMetadata> EvaluatePolicy<TModel, TMetadata>(
        IOperationFold<TModel, TMetadata> root,
        TModel model) =>
        (PolicyResultBase<TMetadata>)Evaluate(root, model);

    /// <summary>
    /// Evaluates <paramref name="root" /> for its outcome alone, allocating no results.
    /// </summary>
    internal static bool Matches<TModel, TMetadata>(
        IOperationFold<TModel, TMetadata> root,
        TModel model) =>
        Fold<TModel, TMetadata, bool, MatchDriver<TModel, TMetadata>>(root, model);

    /// <summary>
    /// The one walk. <typeparamref name="TDriver" /> is a value type, so the JIT specialises this method
    /// per fold and the calls through it are direct — the two folds share their control flow without
    /// paying for the abstraction that lets them.
    /// </summary>
    private static TValue Fold<TModel, TMetadata, TValue, TDriver>(
        IOperationFold<TModel, TMetadata> root,
        TModel model)
        where TDriver : struct, IFoldDriver<TModel, TMetadata, TValue>
    {
        var driver = default(TDriver);

        // Claimed before the buffer so that a nested fold refused on entry — the decorator layer that
        // spends the last of its caller's budget — leaves nothing to return.
        using var budget = EvaluationBudget.Enter();

        var frames = FrameBuffer<TModel, TMetadata, TValue>.Take();
        var deepest = 1;

        try
        {
            frames[0] = new Frame<TModel, TMetadata, TValue>(root);
            var depth = 1;

            TValue completed = default!;
            var hasCompleted = false;

            while (true)
            {
                ref var frame = ref frames[depth - 1];

                if (hasCompleted)
                {
                    frame.Accept(completed);
                    hasCompleted = false;
                }

                var next = frame.NextOperand(driver);

                if (next is null)
                {
                    var value = driver.Combine(frame.Node, frame.First, frame.Second, frame.HasSecond);

                    if (--depth == 0)
                        return value;

                    completed = value;
                    hasCompleted = true;
                    continue;
                }

                EvaluationBudget.Charge();

                if (next is IOperationFold<TModel, TMetadata> operation)
                {
                    if (depth == frames.Length)
                        Array.Resize(ref frames, depth * 2);

                    frames[depth++] = new Frame<TModel, TMetadata, TValue>(operation);

                    if (depth > deepest)
                        deepest = depth;

                    continue;
                }

                completed = driver.Leaf(next, model);
                hasCompleted = true;
            }
        }
        finally
        {
            FrameBuffer<TModel, TMetadata, TValue>.Return(frames, deepest);
        }
    }

    /// <summary>
    /// One reusable frame buffer per thread, per fold. Without it the shallow compositions that make up
    /// nearly all evaluation would pay an array allocation each time — and
    /// <see cref="SpecBase{TModel}.Matches" />, whose contract is that it allocates nothing, would stop
    /// being free.
    /// </summary>
    /// <remarks>
    /// The buffer is taken rather than borrowed: a fold that calls into an operand's own evaluation can
    /// re-enter this one, and a nested fold that found the same array would overwrite the frames its
    /// caller is still unwinding. A nested fold finds nothing and allocates, which is correct and rare.
    /// </remarks>
    private static class FrameBuffer<TModel, TMetadata, TValue>
    {
        [ThreadStatic] private static Frame<TModel, TMetadata, TValue>[]? _buffer;

        internal static Frame<TModel, TMetadata, TValue>[] Take()
        {
            var buffer = _buffer;

            if (buffer is null)
                return new Frame<TModel, TMetadata, TValue>[InitialCapacity];

            _buffer = null;
            return buffer;
        }

        internal static void Return(Frame<TModel, TMetadata, TValue>[] buffer, int used)
        {
            if (buffer.Length > MaxCachedCapacity)
                return;

            // The frames hold onto operations and their results; a cached buffer that kept them would
            // pin a whole evaluation's tree until the thread's next fold of the same shape.
            Array.Clear(buffer, 0, used);
            _buffer = buffer;
        }
    }

    /// <summary>
    /// One operation part-way through its operands. At most two values are ever outstanding, so they sit
    /// in the frame rather than in a shared buffer.
    /// </summary>
    private struct Frame<TModel, TMetadata, TValue>(IOperationFold<TModel, TMetadata> node)
    {
        private bool _hasFirst;
        private bool _nextSettled;

        public IOperationFold<TModel, TMetadata> Node { get; } = node;

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

        /// <summary>
        /// The operand to evaluate next, or <c>null</c> when the operation has everything it needs and is
        /// ready to combine.
        /// </summary>
        public SpecBase<TModel, TMetadata>? NextOperand<TDriver>(TDriver driver)
            where TDriver : struct, IFoldDriver<TModel, TMetadata, TValue>
        {
            if (!_hasFirst)
                return Node.FirstOperand;

            if (_nextSettled)
                return null;

            _nextSettled = true;
            return Node.NextOperand(driver.Satisfied(First));
        }
    }

    /// <summary>The three things that differ between folding results and folding outcomes.</summary>
    private interface IFoldDriver<TModel, TMetadata, TValue>
    {
        TValue Leaf(SpecBase<TModel, TMetadata> spec, TModel model);

        bool Satisfied(TValue value);

        TValue Combine(IOperationFold<TModel, TMetadata> node, TValue first, TValue second, bool hasSecond);
    }

    private readonly struct ResultDriver<TModel, TMetadata>
        : IFoldDriver<TModel, TMetadata, BooleanResultBase<TMetadata>>
    {
        public BooleanResultBase<TMetadata> Leaf(SpecBase<TModel, TMetadata> spec, TModel model) =>
            spec.EvaluateInternal(model);

        public bool Satisfied(BooleanResultBase<TMetadata> value) => value.Satisfied;

        public BooleanResultBase<TMetadata> Combine(
            IOperationFold<TModel, TMetadata> node,
            BooleanResultBase<TMetadata> first,
            BooleanResultBase<TMetadata> second,
            bool hasSecond) =>
            node.Combine(first, hasSecond ? second : null);
    }

    private readonly struct MatchDriver<TModel, TMetadata> : IFoldDriver<TModel, TMetadata, bool>
    {
        public bool Leaf(SpecBase<TModel, TMetadata> spec, TModel model) => spec.Matches(model);

        public bool Satisfied(bool value) => value;

        public bool Combine(
            IOperationFold<TModel, TMetadata> node,
            bool first,
            bool second,
            bool hasSecond) =>
            node.CombineMatches(first, hasSecond ? second : null);
    }
}
