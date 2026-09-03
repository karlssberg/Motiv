using static Motiv.Tests.Traversal.SmallStack;

namespace Motiv.Tests.Traversal;

/// <summary>
/// The measured ceiling of the recursion Spec 3E left standing, held against regression. Every case
/// runs on an explicitly-sized 1 MB thread — the ASP.NET request-thread stack, and the size at which
/// the numbers below were taken. On the 8 MB main stack they would pass without proving anything.
/// </summary>
/// <remarks>
/// Bisected out-of-process, Release, 1 MB thread — a stack overflow aborts the process rather than
/// throwing, so the ceiling can only be found by a child process's exit code:
/// <code>
/// shape                                     entry point      last depth that returns
/// left-deep And chain (folded, baseline)    Evaluate         ≥ 50,000
/// minimal decorator nest                    Evaluate            9,327
/// explanation decorator nest                Evaluate            7,262
/// minimal decorator nest                    Matches          ≥ 20,000
/// alternating operator / decorator          Evaluate            1,047
/// alternating operator / decorator          Matches             1,235
/// async minimal decorator nest              EvaluateAsync       1,302
/// async alternating                         EvaluateAsync         261
/// AndConcurrently nest                      EvaluateAsync         669
/// AndConcurrently nest                      MatchesAsync        1,037
/// </code>
/// The alternating shape is an order of magnitude worse than a pure nest because each decorator layer
/// costs a whole fold re-entry — <c>EvaluateInternal</c> → <c>EvaluateSpec</c> → <c>Evaluate</c> →
/// <c>Fold</c> → <c>Combine</c> — rather than one wrapper frame.
/// <para>
/// Debug frames are fatter, and Debug is what this suite runs in: the same bisection there gives 876,
/// 232 and 574 for the three shapes covered below. Each case sits at roughly a quarter of its
/// <em>Debug</em> ceiling — a 3.4× margin — so a change that makes a frame fatter fails a test rather
/// than shipping, while ordinary platform variation does not. The synchronous depth is also the one
/// that matters in practice: it is four times <c>RuleSerializerOptions.MaxDocumentDepth</c>'s default
/// of 64, which is as deep as a single rule document can nest.
/// </para>
/// </remarks>
/// <remarks>
/// In the <see cref="MotivLimitsTestCollection" /> despite changing no limit: <c>Concurrent(160)</c>
/// composes well past the 100 that <see cref="DecoratorSeamTests" /> lowers the process-wide
/// <see cref="MotivLimits.MaxEvaluationSize" /> to, and joining the collection is what keeps that
/// out of view. The collection's <c>DisableParallelization</c> would do it too, from outside — but
/// then this suite's isolation would depend on an attribute on another class.
/// </remarks>
[Collection(MotivLimitsTestCollection.Name)]
public class DecoratorNestingTests
{
    [Fact]
    public void Should_evaluate_an_alternating_operator_and_decorator_composition() =>
        OnASmallStack(() => Alternating(256).Evaluate(2).Satisfied.ShouldBeTrue());

    [Fact]
    public void Should_match_an_alternating_operator_and_decorator_composition() =>
        OnASmallStack(() => Alternating(256).Matches(2).ShouldBeTrue());

    /// <summary>
    /// The lowest ceiling of the set by a factor of four, because an async state-machine frame is far
    /// fatter than a call frame — the same reason <c>EvaluateAsync</c> was Spec 3E's worst of three.
    /// </summary>
    [Fact]
    public void Should_evaluate_an_alternating_async_composition() =>
        OnASmallStack(() => AsyncAlternating(64)
            .EvaluateAsync(2).AsTask().GetAwaiter().GetResult().Satisfied.ShouldBeTrue());

    /// <summary>
    /// The other recursion Spec 3E named: <c>AndConcurrently</c> fans out through
    /// <c>Task.WhenAll</c> rather than walking, so the fold leaves it to evaluate itself. Unreachable
    /// from a rule document — <c>Motiv.Serialization</c>'s <c>RuleOperator</c> has no concurrent member —
    /// so this depth is whatever an author writes by hand.
    /// </summary>
    [Fact]
    public void Should_evaluate_a_nest_of_concurrent_operators() =>
        OnASmallStack(() => Concurrent(160)
            .EvaluateAsync(2).AsTask().GetAwaiter().GetResult().Satisfied.ShouldBeTrue());

    private static SpecBase<int, string> Leaf(int index) =>
        Spec.Build((int n) => n % 2 == 0).Create($"p{index} is even");

    /// <summary>
    /// One operator level and one decorator level per layer — the shape <c>RuleBinder</c> composes,
    /// since <c>Decorate</c> wraps every node carrying a <c>name</c> or a <c>whenTrue</c>.
    /// </summary>
    private static SpecBase<int, string> Alternating(int layers)
    {
        var spec = Leaf(0);
        for (var layer = 0; layer < layers; layer++)
            spec = Spec.Build(spec.And(Leaf(layer + 1))).Create($"layer{layer}");

        return spec;
    }

    private static AsyncSpecBase<int, string> AsyncAlternating(int layers)
    {
        var spec = Leaf(0).ToAsyncSpec();
        for (var layer = 0; layer < layers; layer++)
            spec = Spec.Build(spec.And(Leaf(layer + 1).ToAsyncSpec())).Create($"layer{layer}");

        return spec;
    }

    private static AsyncSpecBase<int, string> Concurrent(int layers)
    {
        var spec = Leaf(0).ToAsyncSpec();
        for (var layer = 0; layer < layers; layer++)
            spec = spec.AndConcurrently(Leaf(layer + 1).ToAsyncSpec());

        return spec;
    }
}
