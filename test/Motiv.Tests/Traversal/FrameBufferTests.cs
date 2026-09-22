namespace Motiv.Tests.Traversal;

/// <summary>
/// <see cref="SpecBase{TModel}.Matches" /> allocates nothing. The evaluation fold's per-thread frame
/// buffer exists for that reason and nothing else, so the claim is only as good as the shapes it holds
/// on.
/// </summary>
/// <remarks>
/// It held on one. <see href="https://github.com/karlssberg/Motiv/issues/205">#205</see> measured the
/// alternating operator/decorator shape and found <c>(layers - 1) x 152</c> bytes — one frame array per
/// <em>nested</em> fold, the outermost reusing the cached one. That is the same defect
/// <see href="https://github.com/karlssberg/Motiv/issues/202">#202</see> fixed in
/// <see cref="MotivLimits.MaxEvaluationSize" />, in the sibling property and reachable the same way: a
/// rule document produces one nested fold per decorator layer, so a <c>Matches</c> over one allocated
/// linearly in its decorator depth.
/// <para>
/// The cases that <em>measure</em> are not built for <c>net472</c>, which has no per-thread allocation
/// counter. The one that does not — the guard that a composition deeper than the retention cap still
/// answers correctly — carries on all four targets, because the mistake it refuses is an indexing one
/// and indexing is not framework-specific. <see cref="ReasonCostTests" /> draws its own <c>#if</c> in
/// the same place and for the same reason.
/// </para>
/// <para>
/// Uncollected, deliberately. The fixtures here run to 216 nodes and sibling suites lower
/// <see cref="MotivLimits.MaxEvaluationSize" /> process-wide to as little as 5 — but every class that
/// does so is in <see cref="MotivLimitsTestCollection" />, whose <c>DisableParallelization</c>
/// withdraws it from parallel execution altogether. <see cref="ReasonCostTests" /> composes an
/// 800-operand chain on the same reasoning. The dependency on an attribute of another class is real
/// and knowingly accepted; if it were ever broken these cases would throw a <c>SpecException</c>
/// rather than drift, so the failure would name itself.
/// </para>
/// </remarks>
public class FrameBufferTests
{
#if !NETFRAMEWORK

    /// <summary>
    /// The control, and the only shape the contract was ever held on. Without it the cases below would
    /// pass on a build where <c>Matches</c> had stopped folding at all.
    /// </summary>
    [Fact]
    public void Should_match_a_flat_composition_without_allocating()
    {
        var spec = FlatChain(operands: 16);

        Allocated(spec).ShouldBe(0);
    }

    /// <summary>
    /// Stated at three depths rather than one, because the defect is linear in depth and a single
    /// case cannot tell "no allocation" from "one allocation, at a depth this case does not reach".
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void Should_match_a_decorator_layered_composition_without_allocating(int layers)
    {
        var spec = NestedChain(layers, operandsPerLayer: 4);

        Allocated(spec).ShouldBe(
            0,
            "a nested fold is one fold per decorator layer, and the frame buffer is what makes a fold " +
            "free; a buffer only the outermost fold can reuse makes the contract per fold rather than " +
            "per evaluation");
    }

    /// <summary>
    /// The reuse is capped, because the depth it now spans is not bounded by anything else. #201
    /// measured the alternating ceiling at over a thousand layers, and a buffer retained per layer at
    /// the width bound would be the megabytes-per-thread that <c>MaxCachedCapacity</c> exists to refuse.
    /// So the cap is real, and what it costs past itself is one buffer per layer — the behaviour every
    /// nested fold had before this.
    /// </summary>
    /// <remarks>
    /// Stated as three measurements against each other rather than against a constant: the cases say
    /// <em>nothing up to the cap, one buffer per layer past it</em> without naming what a buffer costs,
    /// so a change to <c>InitialCapacity</c> or to the fold's frame layout cannot make them fail for a
    /// reason that is not this one. The cap itself — sixteen — is the one number they do encode.
    /// </remarks>
    [Fact]
    public void Should_pay_one_buffer_per_layer_past_the_retention_cap_and_nothing_before_it()
    {
        var atTheCap = Allocated(NestedChain(layers: 16, operandsPerLayer: 4));
        var fourPast = Allocated(NestedChain(layers: 20, operandsPerLayer: 4));
        var eightPast = Allocated(NestedChain(layers: 24, operandsPerLayer: 4));

        atTheCap.ShouldBe(0, "sixteen nested folds are sixteen retained buffers");

        (eightPast - fourPast).ShouldBe(
            fourPast - atTheCap,
            "past the cap a fold allocates its own buffer, as every nested fold used to, and the four " +
            "layers from 16 to 20 must cost exactly what the four from 20 to 24 do");

        fourPast.ShouldBeGreaterThan(0, "otherwise the cap is not being reached and nothing is proved");
    }

    /// <summary>
    /// Past the cap, <em>which</em> levels stop being served decides whether the cap is affordable.
    /// The levels that keep their buffers must be the outermost ones: a fold's buffer is as wide as
    /// its operand run, and the outermost run is the one a caller writes by hand and can make wide,
    /// where the levels a decorator chain adds are two frames each.
    /// </summary>
    /// <remarks>
    /// Stated as a wide outer fold costing what a narrow one does, so it names no allocation figure and
    /// holds whatever the cap is. Both shapes have the same number of folds and so the same number of
    /// levels past the cap; the only difference is the width of the outermost, which must be free.
    /// <para>
    /// It is not free under a pool indexed by stack position. Buffers are returned innermost-first, so
    /// the ones a full pool refuses are the <em>outermost</em> — and the wide fold, having popped some
    /// inner level's narrow buffer, walks the whole 8-16-32-64 resize ladder and then has the result
    /// refused, every evaluation. That is worse than the single slot this replaced, which handed the
    /// outermost fold back its own buffer because the outermost was the last to return.
    /// </para>
    /// </remarks>
    [Fact]
    public void Should_keep_the_buffer_of_a_wide_outermost_fold_past_the_retention_cap()
    {
        var narrow = Allocated(WideOverNested(layers: 20, width: 1));
        var wide = Allocated(WideOverNested(layers: 20, width: 60));

        wide.ShouldBe(
            narrow,
            "past the cap the levels that go unserved must be the deepest, not the outermost, so a " +
            "wide outermost fold keeps the buffer it grew rather than regrowing it every evaluation");
    }

#endif

    /// <summary>
    /// A composition deeper than the cap still evaluates, and evaluates correctly. The cap drops a
    /// buffer rather than refusing one, so a mistake there would surface as a wrong answer at depth
    /// rather than as an exception.
    /// </summary>
    [Fact]
    public void Should_evaluate_a_composition_nested_deeper_than_the_retention_cap()
    {
        var spec = NestedChain(layers: 24, operandsPerLayer: 4);

        spec.Evaluate(2).Satisfied.ShouldBeTrue();
        spec.Evaluate(3).Satisfied.ShouldBeFalse();
        spec.Matches(2).ShouldBeTrue();
        spec.Matches(3).ShouldBeFalse();
    }

#if !NETFRAMEWORK

    /// <summary>
    /// Matches once to warm the thread — the first call JITs the fold and fills its frame buffers —
    /// then measures the second.
    /// </summary>
    private static long Allocated(SpecBase<int, string> spec)
    {
        _ = spec.Matches(2);

        var before = GC.GetAllocatedBytesForCurrentThread();
        _ = spec.Matches(2);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

#endif

    private static SpecBase<int, string> Leaf(int index) =>
        Spec.Build((int n) => n % 2 == 0).Create($"p{index} is even");

    private static SpecBase<int, string> FlatChain(int operands) =>
        Enumerable.Range(0, operands).Select(Leaf).Aggregate((left, right) => left.And(right));

    /// <summary>
    /// The alternating shape, restated here rather than shared with <see cref="DecoratorSeamTests" />
    /// so a change to one suite's fixture cannot silently move the other's arithmetic.
    /// </summary>
    private static SpecBase<int, string> NestedChain(int layers, int operandsPerLayer)
    {
        var spec = Leaf(0);
        for (var layer = 0; layer < layers; layer++)
        {
            var inner = spec;
            for (var operand = 0; operand < operandsPerLayer; operand++)
                inner = inner.And(Leaf(operand));
            spec = Spec.Build(inner).Create($"layer{layer}");
        }

        return spec;
    }

    /// <summary>
    /// A nested chain with an operand run of <paramref name="width" /> laid over the top of it, so that
    /// the outermost fold is the wide one and every fold beneath it is two frames deep. Sixty stays
    /// inside <c>MaxCachedCapacity</c>, so what the case measures is the cap's choice of which levels
    /// to serve rather than the width bound refusing the buffer outright.
    /// </summary>
    private static SpecBase<int, string> WideOverNested(int layers, int width)
    {
        var spec = NestedChain(layers, operandsPerLayer: 1);
        for (var operand = 0; operand < width; operand++)
            spec = spec.And(Leaf(operand));

        return spec;
    }
}
