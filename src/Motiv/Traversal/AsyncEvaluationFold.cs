namespace Motiv.Traversal;

/// <summary>
/// The asynchronous counterpart of <see cref="EvaluationFold" />. Same frame machine, awaiting each
/// operand rather than calling it.
/// </summary>
/// <remarks>
/// Two things differ from the synchronous driver, both forced by <c>async</c>:
/// <list type="bullet">
/// <item>Frames are addressed by index rather than through a <c>ref</c> local, because an async method
/// cannot hold a by-ref local across an <c>await</c>. Array element access is still a variable, so the
/// frames are mutated in place either way.</item>
/// <item>There is no per-thread frame buffer. A continuation may resume on a different thread than the
/// one that started the fold, so a thread-static buffer would be returned to the wrong thread — and an
/// async evaluation already allocates a state machine per operand it awaits, next to which one array is
/// not the cost worth chasing.</item>
/// </list>
/// </remarks>
internal static class AsyncEvaluationFold
{
    private const int InitialCapacity = 8;

    /// <summary>Evaluates <paramref name="root" />, producing the composed result.</summary>
    internal static ValueTask<BooleanResultBase<TMetadata>> EvaluateAsync<TModel, TMetadata>(
        IAsyncOperationFold<TModel, TMetadata> root,
        TModel model,
        CancellationToken cancellationToken) =>
        FoldAsync<TModel, TMetadata, BooleanResultBase<TMetadata>, ResultDriver<TModel, TMetadata>>(
            root, model, cancellationToken);

    /// <summary>
    /// Evaluates <paramref name="root" />, producing the composed policy result. The cast is safe for the
    /// reason <see cref="EvaluationFold.EvaluatePolicy{TModel,TMetadata}" /> gives.
    /// </summary>
    internal static async ValueTask<PolicyResultBase<TMetadata>> EvaluatePolicyAsync<TModel, TMetadata>(
        IAsyncOperationFold<TModel, TMetadata> root,
        TModel model,
        CancellationToken cancellationToken) =>
        (PolicyResultBase<TMetadata>)await EvaluateAsync(root, model, cancellationToken).ConfigureAwait(false);

    /// <summary>Evaluates <paramref name="root" /> for its outcome alone, composing no results.</summary>
    internal static ValueTask<bool> MatchesAsync<TModel, TMetadata>(
        IAsyncOperationFold<TModel, TMetadata> root,
        TModel model,
        CancellationToken cancellationToken) =>
        FoldAsync<TModel, TMetadata, bool, MatchDriver<TModel, TMetadata>>(root, model, cancellationToken);

    private static async ValueTask<TValue> FoldAsync<TModel, TMetadata, TValue, TDriver>(
        IAsyncOperationFold<TModel, TMetadata> root,
        TModel model,
        CancellationToken cancellationToken)
        where TDriver : struct, IAsyncFoldDriver<TModel, TMetadata, TValue>
    {
        var driver = default(TDriver);
        var frames = new Frame<TModel, TMetadata, TValue>[InitialCapacity];
        frames[0] = new Frame<TModel, TMetadata, TValue>(root);
        var depth = 1;
        var size = 1;

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
                    frames[index].Node,
                    frames[index].First,
                    frames[index].Second,
                    frames[index].HasSecond);

                if (--depth == 0)
                    return value;

                completed = value;
                hasCompleted = true;
                continue;
            }

            if (++size > MotivLimits.MaxEvaluationSize)
                throw new SpecException(
                    $"The evaluation exceeded the maximum size of {MotivLimits.MaxEvaluationSize} nodes. " +
                    "Compose fewer propositions, or raise " +
                    $"{nameof(MotivLimits)}.{nameof(MotivLimits.MaxEvaluationSize)}.");

            if (next is IAsyncOperationFold<TModel, TMetadata> { IsConcurrent: false } operation)
            {
                if (depth == frames.Length)
                    Array.Resize(ref frames, depth * 2);

                frames[depth++] = new Frame<TModel, TMetadata, TValue>(operation);
                continue;
            }

            completed = await driver.LeafAsync(next, model, cancellationToken).ConfigureAwait(false);
            hasCompleted = true;
        }
    }

    private struct Frame<TModel, TMetadata, TValue>(IAsyncOperationFold<TModel, TMetadata> node)
    {
        private bool _hasFirst;
        private bool _nextSettled;

        public IAsyncOperationFold<TModel, TMetadata> Node { get; } = node;

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
                return Node.FirstOperand;

            if (_nextSettled)
                return null;

            _nextSettled = true;
            return Node.NextOperand(driver.Satisfied(First));
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
            IAsyncOperationFold<TModel, TMetadata> node,
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
            IAsyncOperationFold<TModel, TMetadata> node,
            BooleanResultBase<TMetadata> first,
            BooleanResultBase<TMetadata> second,
            bool hasSecond) =>
            node.Combine(first, hasSecond ? second : null);
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
            IAsyncOperationFold<TModel, TMetadata> node,
            bool first,
            bool second,
            bool hasSecond) =>
            node.CombineMatches(first, hasSecond ? second : null);
    }
}
