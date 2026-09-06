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

    /// <summary>
    /// How many nesting levels keep a buffer for reuse — a count, like
    /// <see cref="MaxCachedCapacity" />, so the deepest level served is one below it. Not a bound on how
    /// deep a composition may nest: a fold below it allocates, exactly as every nested fold used to.
    /// </summary>
    /// <remarks>
    /// The width bound is what sets this one. A thread used to retain a single buffer, so at the worst
    /// case where it had grown to <see cref="MaxCachedCapacity" /> it held around two kilobytes per
    /// instantiation; retaining one buffer per level takes that to about thirty-two kilobytes — a
    /// sixteenfold rise, and about sixty-four times below the two megabytes
    /// <see cref="MaxCachedCapacity" /> exists to refuse. Left unbounded it would reach exactly that figure:
    /// <see href="https://github.com/karlssberg/Motiv/issues/201">#201</see> measured the alternating
    /// ceiling at over a thousand layers.
    /// <para>
    /// The figure is per <em>instantiation</em>, and neither bound limits how many of those a process
    /// has — the pool is a static of a generic type, so a thread that evaluates over many model and
    /// metadata types holds a pool per closed type it touched. That axis existed before this change at
    /// a sixteenth of the size, and is the one an application can grow without composing anything
    /// deeply.
    /// </para>
    /// </remarks>
    private const int MaxCachedDepth = 16;

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
    /// Reusable frame buffers, per thread, one per nesting level. Without them the shallow compositions
    /// that make up nearly all evaluation would pay an array allocation each time — and
    /// <see cref="SpecBase{TModel}.Matches" />, whose contract is that it allocates nothing, would stop
    /// being free.
    /// </summary>
    /// <remarks>
    /// A fold that calls into an operand's own evaluation can re-enter this one, and a nested fold
    /// handed the same array would overwrite the frames its caller is still unwinding. That hazard is
    /// what one buffer per level answers — two live folds are at two levels, so they read two slots.
    /// Taking rather than borrowing answers a different question, and <see cref="Take" /> says which.
    /// <para>
    /// With a single slot the nested fold found nothing and allocated, which the remarks here called
    /// "correct and rare". It is correct and it is not rare:
    /// <see href="https://github.com/karlssberg/Motiv/issues/205">#205</see> measured
    /// <c>(layers - 1) x 152</c> bytes on the alternating operator/decorator shape, and
    /// <c>RuleBinder.Decorate</c> wraps every node carrying a <c>name</c> or a <c>whenTrue</c> — so a
    /// <c>Matches</c> over a document-composed rule allocated linearly in its decorator depth, and the
    /// allocation-free contract held per <em>fold</em> rather than per evaluation. That is the same
    /// defect <see href="https://github.com/karlssberg/Motiv/issues/202">#202</see> fixed in
    /// <see cref="MotivLimits.MaxEvaluationSize" />, in the sibling property.
    /// </para>
    /// <para>
    /// <b>Indexed by nesting level, not by stack position.</b> Both serve every fold up to the cap, and
    /// they differ only past it — in which levels stop being served, which is the whole question. A
    /// stack refuses the buffers returned last, and folds return innermost-first, so it would refuse the
    /// <em>outermost</em>: the one level whose operand run a caller writes by hand and can make wide,
    /// where the levels a decorator chain adds are two frames each. A wide outermost fold would pop some
    /// inner level's narrow array, walk the whole resize ladder, and have the result refused — every
    /// evaluation, and worse than the single slot this replaced, which at least handed the outermost
    /// fold its own buffer back. Indexing by level inverts that: level <c>d</c> is served level
    /// <c>d</c>'s own array at every depth, and what goes unserved past the cap is the deep tail.
    /// </para>
    /// <para>
    /// The region-based alternative #205 sketched — one array partitioned by the caller's high-water
    /// mark — is not in fact contained here. A nested fold that grew the shared array would leave the
    /// caller's <c>frames</c> local pointing at the array before the resize, so the outer fold would
    /// unwind against a stale copy; keeping it correct means re-reading the buffer after every leaf, on
    /// the hot path, which is a change to the fold rather than to its buffer.
    /// </para>
    /// </remarks>
    private static class FrameBuffer<TModel, TMetadata, TValue>
    {
        /// <summary>
        /// Slot <c>d</c> holds the buffer nesting level <c>d</c> last returned, or <c>null</c> where
        /// that level has none to hand out. Null until the thread's first <see cref="Return" />.
        /// </summary>
        [ThreadStatic] private static Frame<TModel, TMetadata, TValue>[]?[]? _buffers;

        /// <summary>
        /// Folds of <em>this instantiation</em> in flight on this thread, which is the nesting level the
        /// next one enters at. A re-entry through <c>ChangeModelTo</c> lands in a different closed type
        /// with a pool and a count of its own, and enters it at level zero — correct, since separate
        /// pools cannot alias, but it is not a count of the thread's folds.
        /// </summary>
        /// <remarks>
        /// <see cref="Take" /> and <see cref="Return" /> are the only writers and are paired by the
        /// fold's <c>try</c>/<c>finally</c> — <see cref="Take" /> immediately precedes the <c>try</c> and
        /// <see cref="Return" /> is its <c>finally</c> — so once a fold has entered, this returns to zero
        /// however that fold leaves. The gap is an <see cref="OutOfMemoryException" /> from
        /// <see cref="Take" />'s own allocation, which would leave the count high by one for the life of
        /// the thread; the pool would then stop serving that instantiation, and nothing would alias,
        /// because a taken slot is nulled regardless of what the count says.
        /// </remarks>
        [ThreadStatic] private static int _depth;

        internal static Frame<TModel, TMetadata, TValue>[] Take()
        {
            var level = _depth++;
            var buffers = _buffers;

            if (level >= MaxCachedDepth || buffers is null)
                return new Frame<TModel, TMetadata, TValue>[InitialCapacity];

            var buffer = buffers[level];

            if (buffer is null)
                return new Frame<TModel, TMetadata, TValue>[InitialCapacity];

            // Taken rather than borrowed, at the level as well as at the thread: a slot still naming a
            // buffer that is never returned — one grown past MaxCachedCapacity — would pin it for the
            // life of the thread.
            buffers[level] = null;
            return buffer;
        }

        internal static void Return(Frame<TModel, TMetadata, TValue>[] buffer, int used)
        {
            var level = --_depth;

            if (level >= MaxCachedDepth || buffer.Length > MaxCachedCapacity)
                return;

            // The frames hold onto operations and their results; a cached buffer that kept them would
            // pin a whole evaluation's tree until the thread's next fold at this level.
            Array.Clear(buffer, 0, used);

            var buffers = _buffers ??= new Frame<TModel, TMetadata, TValue>[MaxCachedDepth][];
            buffers[level] = buffer;
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
