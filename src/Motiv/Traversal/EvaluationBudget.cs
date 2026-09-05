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
/// 250,000-element collection is not a 250,000-node
/// composition. The fold cannot tell the two re-entries apart, so the higher-order funnels say so
/// themselves through <see cref="OutsideBudget{TArgument,TState,TResult}" /> — see
/// <c>HigherOrderResults</c> and <c>HigherOrderShortCircuit</c>, the only two places in the library
/// where an element is resolved.
/// </para>
/// <para>
/// <b>Why a thread-static.</b> <see cref="SpecBase{TModel}.Evaluate" /> and
/// <see cref="SpecBase{TModel}.Matches" /> never leave the thread that started them, so a thread-static
/// is both correct and free — and <c>Matches</c> allocates nothing, which is a contract Spec 3E paid for
/// with a per-thread frame buffer and this must not spend. The asynchronous fold cannot use one: a
/// continuation may resume on a thread whose slot holds a suspended evaluation's count. It therefore
/// still bounds one fold, tracked as
/// <see href="https://github.com/karlssberg/Motiv/issues/204">#204</see>.
/// </para>
/// </remarks>
internal static class EvaluationBudget
{
    /// <summary>
    /// Nodes charged to the evaluation in flight on this thread. Zero means no budget is in force, so
    /// the next <see cref="Enter" /> is the outermost one and takes <see cref="Ownership" /> of it.
    /// </summary>
    [ThreadStatic] private static int _spent;

    /// <summary>
    /// Charges the fold's root node and returns its <see cref="Ownership" /> of the budget. A fold
    /// entered while another is unwinding — the decorator case — spends the caller's budget and owns
    /// nothing.
    /// </summary>
    internal static Ownership Enter()
    {
        var isRoot = _spent == 0;
        Charge();
        return new Ownership(isRoot);
    }

    /// <summary>Charges one node, abandoning the evaluation when the bound is passed.</summary>
    internal static void Charge()
    {
        if (++_spent > MotivLimits.MaxEvaluationSize)
            ThrowExceeded();
    }

    /// <summary>
    /// The refusal itself, kept out of <see cref="Charge" />. The charge replaced an increment that was
    /// inline in the fold's loop and is made once per node; leaving the message's interpolation in its
    /// body would weigh against the size the JIT is willing to inline, for a branch never taken.
    /// </summary>
    private static void ThrowExceeded() =>
        throw new SpecException(
            $"The evaluation exceeded the maximum size of {MotivLimits.MaxEvaluationSize} nodes. " +
            "Compose fewer propositions, or raise " +
            $"{nameof(MotivLimits)}.{nameof(MotivLimits.MaxEvaluationSize)}.");

    /// <summary>
    /// Derives one value with the budget set aside, so that work done <em>inside</em> a node neither
    /// spends the composition's budget nor goes unbounded itself — whatever <paramref name="work" />
    /// composes is bounded afresh, as though it had been evaluated on its own.
    /// </summary>
    /// <remarks>
    /// An invocation rather than a second <c>using</c> scope: both callers exclude exactly one
    /// projection, and holding the rule here rather than as a helper apiece keeps it in one place should
    /// a third element-resolving call site ever appear. <paramref name="state" /> is threaded through
    /// rather than captured so those callers can keep handing over a non-capturing <c>static</c> lambda.
    /// <para>
    /// The count is set aside and handed back rather than released, which is what separates this from
    /// <see cref="Ownership" />: the composition that was in flight resumes counting from where it left
    /// off, so a hundred elements cost it what one does — nothing.
    /// </para>
    /// </remarks>
    internal static TResult OutsideBudget<TArgument, TState, TResult>(
        TArgument argument,
        TState state,
        Func<TArgument, TState, TResult> work)
    {
        var outer = _spent;
        _spent = 0;

        try
        {
            return work(argument, state);
        }
        finally
        {
            _spent = outer;
        }
    }

    /// <summary>
    /// Answers one question for the fold holding it: <b>when I end, does the evaluation end?</b> Only
    /// the outermost fold — the one that found no budget in force — can say yes, and only it releases
    /// the count. That happens however the fold leaves, so a refused evaluation cannot leave its
    /// spending behind for the next caller on the thread.
    /// </summary>
    /// <remarks>
    /// The flag is the load-bearing part, and the two tidier-looking encodings are both wrong. Making
    /// every fold release would <em>refund</em> a decorator's subtree, so sibling subtrees would each
    /// get the whole allowance back. Restoring the count this fold entered with does the same thing
    /// more subtly, charging a whole nested subtree as one node. Either one silently reinstates the
    /// per-fold bound that #202 removed: the fifty-layer composition that must be refused at 100 peaks
    /// at about 21 under both, rather than reaching 1,050.
    /// <para>
    /// Distinct from <see cref="OutsideBudget{TArgument,TState,TResult}" />, which it resembles and is
    /// not. This asks whether an evaluation is <i>over</i>; that one asserts a span of work was never
    /// <i>part of</i> the evaluation.
    /// </para>
    /// </remarks>
    internal ref struct Ownership(bool isRoot)
    {
        public void Dispose()
        {
            if (isRoot)
                _spent = 0;
        }
    }
}
