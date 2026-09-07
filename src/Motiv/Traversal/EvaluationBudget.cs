namespace Motiv.Traversal;

/// <summary>
/// The running cost of one evaluation, against <see cref="MotivLimits.MaxEvaluationSize" />.
/// </summary>
/// <remarks>
/// The budget is ambient rather than a parameter because it has to survive a trip through code that
/// cannot carry it. <see cref="EvaluationFold" /> folds operations and evaluates everything else —
/// decorators among them — through <c>EvaluateInternal</c>, which lands in an override of the
/// <c>protected abstract</c> <see cref="SpecBase{TModel,TMetadata}.EvaluateSpec" />. Threading a
/// parameter to where a decorator re-enters the fold would mean changing that signature, which every
/// user-defined <c>Spec</c> subclass implements.
/// <para>
/// <b>Nesting is inheritance, and that is the whole point.</b> A decorator between two operator layers
/// re-enters the fold; before
/// <see href="https://github.com/karlssberg/Motiv/issues/202">#202</see> the count lived in a fold-local
/// and every re-entry started a fresh one, so the bound applied per fold rather than per evaluation and
/// fifty decorator layers of ten operands passed a limit of a hundred.
/// </para>
/// <para>
/// <b>The exclusion is documented, and declared rather than detected.</b> A higher-order proposition
/// resolves its inner spec once per element through that same entry point, and
/// <see cref="MotivLimits.MaxEvaluationSize" /> has always promised such work is not counted — a
/// 250,000-element collection is not a 250,000-node composition. The fold cannot tell the two
/// re-entries apart, so the places that resolve elements say so themselves through
/// <see cref="Exclude" /> — <c>HigherOrderResults</c>, <c>HigherOrderShortCircuit</c> and
/// <c>EnumerableExtensions.Where</c>. The same declaration covers a <c>Tap</c> callback, which is a
/// side effect hung off a node rather than part of the decision the node makes, and
/// <c>EvaluationScope</c>, which is that argument applied to observability:
/// <see href="https://github.com/karlssberg/Motiv/issues/209">#209</see>.
/// </para>
/// <para>
/// <b>The unit a seam declares is the node's whole decision, not its elements.</b>
/// <c>HigherOrderResults</c> excluded the projection and stopped there, so a predicate supplied
/// through <c>As(...)</c> that evaluated a proposition of its own was charged: the elements were
/// excluded while the answer they were resolved for was not
/// (<see href="https://github.com/karlssberg/Motiv/issues/208">#208</see>). It now materializes and
/// applies the predicate under one scope, and does not offer the halves separately, because a list of
/// seams maintained by hand is only as good as its least careful copy — and that one had nineteen.
/// </para>
/// <para>
/// <b>The same node's <em>result</em> declares its lazy half, and the reason is a different one.</b>
/// A higher-order result defers its cause selector and its <c>WhenTrue</c>/<c>WhenFalse</c> to first
/// property read, so each ran on whatever evaluation was in flight then — and, being memoized, on
/// whichever one reached the property first, which made the same composition acceptable or refused
/// depending on whether a logger had already looked at it
/// (<see href="https://github.com/karlssberg/Motiv/issues/213">#213</see>). They are excluded not
/// because they are inside the node's decision but because they come <em>after</em> it: the result is
/// handed <c>Satisfied</c> when it is constructed, so these delegates describe a decision rather than
/// reach one. <c>HigherOrderResults.ResolveCauses</c>, <c>ResolveValue</c> and <c>ResolveValues</c>
/// own that scope, covered by the same gate.
/// </para>
/// <para>
/// <b>Two carriers, because an evaluation is bounded by whichever one it is running under.</b>
/// <see cref="SpecBase{TModel}.Evaluate" /> and <see cref="SpecBase{TModel}.Matches" /> never leave the
/// thread that started them, so a thread-static is both correct and free — and <c>Matches</c> allocates
/// nothing, which is a contract Spec 3E paid for with a per-thread frame buffer and this must not spend.
/// An asynchronous evaluation cannot use one: a continuation may resume on a thread whose slot holds a
/// <em>suspended</em> evaluation's count — not a stale one, a live one — so two interleaved evaluations
/// would corrupt each other's counts in both directions. It therefore carries a <see cref="Counter" />
/// in an <see cref="AsyncLocal{T}" />, which flows into continuations and into the branches of a
/// concurrent fan-out (<see href="https://github.com/karlssberg/Motiv/issues/204">#204</see>).
/// </para>
/// <para>
/// <b>The flowed counter outranks the thread-static, and that is what keeps the seam from moving.</b>
/// An asynchronous composition reaches a synchronous one through <c>SyncSpecAsyncAdapter</c>, which runs
/// a synchronous fold inside the asynchronous evaluation. Were that fold to read only its own
/// thread-static it would start a fresh budget, and a caller could spend twice the bound by putting half
/// the composition behind <c>ToAsyncSpec()</c> — the per-fold defect relocated rather than removed. So
/// <see cref="Enter" /> resolves the flowed counter first and charges it when one is in force.
/// </para>
/// <para>
/// <b>The resolution is per fold, not per node.</b> Every entry point hands back an
/// <see cref="Ownership" /> that already holds the counter it resolved, so the charge made once per node
/// costs a predicted branch rather than an <see cref="AsyncLocal{T}" /> read.
/// </para>
/// </remarks>
internal static class EvaluationBudget
{
    /// <summary>
    /// Nodes charged to the synchronous evaluation in flight on this thread. Zero means no synchronous
    /// budget is in force, so the next <see cref="Enter" /> is the outermost one and takes
    /// <see cref="Ownership" /> of it.
    /// </summary>
    [ThreadStatic] private static int _spent;

    /// <summary>
    /// The count of the asynchronous evaluation this execution context belongs to, or <c>null</c> when
    /// none is in force. A reference rather than an <see cref="int" /> because a write per node through
    /// <see cref="AsyncLocal{T}" /> would copy the execution context per node; the slot is written once
    /// per evaluation and the box mutated thereafter.
    /// </summary>
    private static readonly AsyncLocal<Counter?> Flowing = new();

    /// <summary>
    /// Charges the fold's root node and returns its <see cref="Ownership" /> of the budget. A fold
    /// entered while another is unwinding — the decorator case — spends the caller's budget and owns
    /// nothing.
    /// </summary>
    /// <remarks>
    /// The root is charged unconditionally because it is a node no one else has charged: a nested fold's
    /// root is the spec a decorator wraps, and the fold above charged the decorator rather than it.
    /// <see cref="EnterFanOut" /> is the one entry where that is not true.
    /// </remarks>
    internal static Ownership Enter()
    {
        var flowing = Flowing.Value;
        if (flowing is null)
        {
            var isRoot = _spent == 0;
            Charge();
            return new Ownership(counter: null, owned: isRoot);
        }

        Charge(flowing);
        return new Ownership(flowing, owned: false);
    }

    /// <summary>
    /// The asynchronous fold's entry. Establishes the flowed <see cref="Counter" /> when none is in
    /// force, charges the fold's root node, and hands back the counter so the per-node charge needs no
    /// further lookup.
    /// </summary>
    /// <remarks>
    /// <b>The slot is written from an asynchronous method's synchronous prefix, and that is load-bearing
    /// in both directions.</b> Everything the fold goes on to await sees the counter, because the
    /// continuation captures the execution context this write produced; and the caller does not, because
    /// <c>AsyncTaskMethodBuilder.Start</c> restores the thread's execution context once the first
    /// <c>MoveNext</c> returns. The release is therefore the runtime's rather than ours — stated anyway
    /// in <see cref="Ownership.Dispose" />, and pinned by behaviour in
    /// <c>AsyncEvaluationBudgetTests.Should_start_each_async_evaluation_with_its_whole_budget</c>.
    /// </remarks>
    internal static Ownership EnterAsync()
    {
        var flowing = Flowing.Value;
        if (flowing is null)
            return BeginFlowing();

        Charge(flowing);
        return new Ownership(flowing, owned: false);
    }

    /// <summary>
    /// The entry for a concurrent operator's fan-out, which is not a fold: it evaluates both operands at
    /// once and each enters a fold of its own, so without a counter reachable from both the bound would
    /// apply per branch.
    /// </summary>
    /// <remarks>
    /// It differs from <see cref="EnterAsync" /> in one respect, and the difference is which node is
    /// being entered. A fold's root is the node <em>below</em> whatever reached it; a fan-out's root is
    /// the concurrent node itself, which the fold above already charged as an operand. Charging here as
    /// well would count it twice — so it is charged only when nothing reached it, which is to say when
    /// the concurrent node is the whole evaluation.
    /// </remarks>
    internal static Ownership EnterFanOut()
    {
        var flowing = Flowing.Value;
        if (flowing is null)
            return BeginFlowing();

        return new Ownership(flowing, owned: false); // Deliberately uncharged; see the remarks above.
    }

    /// <summary>
    /// Begins an asynchronous evaluation: publishes a fresh <see cref="Counter" /> to the execution
    /// context, charges the node that opened it, and takes ownership so that the count is released
    /// however the evaluation leaves.
    /// </summary>
    private static Ownership BeginFlowing()
    {
        var counter = new Counter();
        Flowing.Value = counter;
        Charge(counter);
        return new Ownership(counter, owned: true);
    }

    /// <summary>Charges one node to the synchronous carrier, abandoning the evaluation when the bound is passed.</summary>
    private static void Charge()
    {
        if (++_spent > MotivLimits.MaxEvaluationSize)
            ThrowExceeded();
    }

    /// <summary>
    /// Charges one node to a flowed counter. Interlocked because a concurrent operator's two branches
    /// share it: <c>AsyncAndSpec</c> and its siblings fan out through <see cref="Task.WhenAll(Task[])" />
    /// and the execution context flows into both, so both reach this box.
    /// </summary>
    /// <remarks>
    /// The bound is checked on every increment rather than sampled. A fan-out can pass it in two branches
    /// at once, and then both are refused and <see cref="Task.WhenAll(Task[])" /> surfaces one — which is
    /// the right outcome: the count only rises within an evaluation, so once it is past the bound every
    /// later charge is past it too, and which branch reports it is not information a caller can use.
    /// </remarks>
    private static void Charge(Counter counter)
    {
        if (Interlocked.Increment(ref counter.Spent) > MotivLimits.MaxEvaluationSize)
            ThrowExceeded();
    }

    /// <summary>
    /// The refusal itself, kept out of the charge. The charge replaced an increment that was
    /// inline in the fold's loop and is made once per node; leaving the message's interpolation in its
    /// body would weigh against the size the JIT is willing to inline, for a branch never taken.
    /// </summary>
    private static void ThrowExceeded() =>
        throw new SpecException(
            $"The evaluation exceeded the maximum size of {MotivLimits.MaxEvaluationSize} nodes. " +
            "Compose fewer propositions, or raise " +
            $"{nameof(MotivLimits)}.{nameof(MotivLimits.MaxEvaluationSize)}.");

    /// <summary>
    /// Sets the composition's budget aside for a span of work done <em>inside</em> a node, so that the
    /// work neither spends that budget nor goes unbounded itself — whatever happens within the scope is
    /// bounded afresh, as though it had been evaluated on its own.
    /// </summary>
    /// <remarks>
    /// The count is set aside and handed back rather than released, which is what separates this from
    /// <see cref="Ownership" />: the composition that was in flight resumes counting from where it left
    /// off, so a hundred elements cost it what one does — nothing.
    /// <para>
    /// <b>Both carriers are parked, and the flowed one is detached rather than zeroed.</b> Zeroing a
    /// counter a concurrent operator's other branch is still charging would discard that branch's
    /// spending on restore. Hiding the slot instead leaves the counter untouched, so the excluded work
    /// falls through to the thread-static and is bounded on its own account while the composition keeps
    /// counting elsewhere. That the two writes can be restored on the same thread is guaranteed by
    /// <see cref="Exclusion" /> being a <c>ref struct</c>: a scope that cannot span an <c>await</c>
    /// cannot resume anywhere else.
    /// </para>
    /// <para>
    /// A looping caller scopes it around the whole loop, <em>enumeration included</em>, rather than
    /// around each element. Wrapping the projection alone left a lazy source's <c>MoveNext</c> outside
    /// the exclusion, so a sequence whose enumerator evaluated a proposition charged the composition
    /// once per element while the same models passed as an array did not — a bound that depended on
    /// whether the caller had written <c>.ToArray()</c>. Producing an element is part of resolving it.
    /// (<c>EnumerableExtensions.Where</c> is the exception, and per element of necessity: its
    /// projection is deferred, so the scope has to live where the evaluation does.)
    /// </para>
    /// <para>
    /// Elements do not accumulate against each other within the span, and nothing here arranges that:
    /// an element's own evaluation enters the fold with the count at zero, so it is a root, and
    /// <see cref="Ownership" /> releases a root's count on the way out. Each element therefore starts
    /// from zero and stays bounded on its own account. A per-element reset here would only restate what
    /// <see cref="Ownership" /> already guarantees — no mutation can tell the two apart.
    /// </para>
    /// </remarks>
    internal static Exclusion Exclude()
    {
        var outer = _spent;
        _spent = 0;

        // Guarded because an AsyncLocal write copies the execution context, and the synchronous
        // callers that dominate this path have no counter to hide.
        var flowing = Flowing.Value;
        if (flowing is not null)
            Flowing.Value = null;

        return new Exclusion(outer, flowing);
    }

    /// <summary>The count of one asynchronous evaluation, shared by every fold and branch within it.</summary>
    internal sealed class Counter
    {
        /// <summary>A field rather than a property so that <see cref="Interlocked" /> can take it by reference.</summary>
        internal int Spent;
    }

    /// <summary>
    /// A span of work the composition is not charged for. Disposal hands the composition's count back.
    /// </summary>
    internal ref struct Exclusion(int outer, Counter? flowing)
    {
        public readonly void Dispose()
        {
            _spent = outer;

            if (flowing is not null)
                Flowing.Value = flowing;
        }
    }

    /// <summary>
    /// The fold's handle on the budget. It answers one question for the fold holding it — <b>when I end,
    /// does the evaluation end?</b> — and carries the carrier that fold resolved on entry, so that the
    /// charge made once per node needs no <see cref="AsyncLocal{T}" /> read of its own. Only the
    /// outermost fold — the one that found no budget in force — can say yes, and only it releases the
    /// count. That happens however the fold leaves, so a refused evaluation cannot leave its spending
    /// behind for the next caller.
    /// </summary>
    /// <remarks>
    /// The flag is the load-bearing part, and the two tidier-looking encodings are both wrong. Making
    /// every fold release would <em>refund</em> a decorator's subtree, so sibling subtrees would each
    /// get the whole allowance back. Restoring the count this fold entered with does the same thing
    /// more subtly, charging a whole nested subtree as one node. Either one silently reinstates the
    /// per-fold bound that #202 removed: the fifty-layer composition that must be refused at 100 peaks
    /// at about 21 under both, rather than reaching 1,050.
    /// <para>
    /// The two fields are independent because the four states they encode are all reachable: a null
    /// <c>counter</c> means the fold charges the thread-static, and
    /// <c>owned</c> means this fold established whichever carrier it holds. A nested
    /// asynchronous fold has a counter and owns nothing; the outermost synchronous fold has no counter
    /// and owns the thread-static. Folding them into one field would need a carrier the synchronous
    /// fold does not have — and giving it one would cost <see cref="SpecBase{TModel}.Matches" /> an
    /// interlocked increment per node, which is the price Spec 3E already refused.
    /// </para>
    /// <para>
    /// Distinct from <see cref="Exclude" />, which it resembles and is not. This asks whether an
    /// evaluation is <i>over</i>; that one asserts a span of work was never <i>part of</i> the
    /// evaluation.
    /// </para>
    /// <para>
    /// A plain <c>struct</c> rather than the <c>ref struct</c> the synchronous fold alone could use: the
    /// asynchronous fold's scope spans its <c>await</c>s, so it lives in a state machine's fields, which a
    /// <c>ref struct</c> may not. <see cref="Exclusion" /> keeps that restriction and relies on it.
    /// </para>
    /// </remarks>
    internal readonly struct Ownership(Counter? counter, bool owned) : IDisposable
    {
        /// <summary>Charges one node to whichever carrier this fold resolved on entry.</summary>
        public void Charge()
        {
            if (counter is null)
                EvaluationBudget.Charge();
            else
                EvaluationBudget.Charge(counter);
        }

        public void Dispose()
        {
            if (!owned)
                return;

            if (counter is null)
                _spent = 0;
            else
                Flowing.Value = null;
        }
    }
}
