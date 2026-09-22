namespace Motiv.Tests.Traversal;

/// <summary>
/// The seam Spec 3E left standing, measured. The evaluation fold descends through operands that are
/// themselves operations and calls <c>EvaluateInternal</c> on operands that are not — decorators
/// among them — so a decorator between two operator layers re-enters the fold rather than being
/// folded into it.
/// </summary>
/// <remarks>
/// Spec 3E's design doc left that bound stated but unmeasured, and ticket
/// <see href="https://github.com/karlssberg/Motiv/issues/145">#145</see> asked for the measurement
/// before any rewrite. These are the facts it turned up, recorded as they behaved so that the
/// follow-ups fixing them turned these red rather than silently passing — which is what
/// <see href="https://github.com/karlssberg/Motiv/issues/202">#202</see> and
/// <see href="https://github.com/karlssberg/Motiv/issues/204">#204</see> then did, the first on the
/// synchronous pair and the second on the asynchronous one. All four now state the bound rather than
/// the hole. The asynchronous side's own arithmetic lives in
/// <see cref="AsyncEvaluationBudgetTests" />; what stays here is the parity claim — <b>the same
/// composition, refused at the same limit, whichever surface evaluates it.</b>
/// </remarks>
[Collection(MotivLimitsTestCollection.Name)]
public class DecoratorSeamTests : IDisposable
{
    private readonly int _previous = MotivLimits.MaxEvaluationSize;

    public void Dispose() => MotivLimits.MaxEvaluationSize = _previous;

    /// <summary>
    /// The control: a flat chain is one fold, and the bound refuses it exactly as documented.
    /// Without this the case below would prove only that the limit was never set.
    /// </summary>
    [Fact]
    public void Should_refuse_a_flat_composition_past_the_size_bound()
    {
        MotivLimits.MaxEvaluationSize = 100;

        var act = () => FlatChain(200).Evaluate(2);

        act.ShouldThrow<SpecException>();
    }

    /// <summary>
    /// <see cref="MotivLimits.MaxEvaluationSize" /> says it bounds "the maximum number of nodes a
    /// single evaluation may compose", and names exactly one exclusion — work done <em>inside</em> a
    /// node, such as a higher-order proposition over a collection. A decorator's operand is not that:
    /// it is part of the same logical composition, and the documentation therefore claims it is
    /// counted.
    /// <para>
    /// It was not, until <see href="https://github.com/karlssberg/Motiv/issues/202">#202</see>: the
    /// fold held its running size in a local, so every re-entry through a decorator started a fresh
    /// count and the bound applied per <em>fold</em>. Fifty layers of ten operands is over a thousand
    /// nodes and passed a limit of a hundred, while the flat chain above was refused at two hundred.
    /// </para>
    /// </summary>
    [Fact]
    public void Should_bound_a_composition_whose_size_is_spread_across_decorator_layers()
    {
        MotivLimits.MaxEvaluationSize = 100;

        var act = () => NestedChain(layers: 50, operandsPerLayer: 10).Evaluate(2);

        act.ShouldThrow<SpecException>();
    }

    /// <summary>The same hole on the allocation-free path, which shared the defect and not the code.</summary>
    [Fact]
    public void Should_bound_a_decorator_layered_match()
    {
        MotivLimits.MaxEvaluationSize = 100;

        var act = () => { _ = NestedChain(layers: 50, operandsPerLayer: 10).Matches(2); };

        act.ShouldThrow<SpecException>();
    }

    /// <summary>
    /// The asynchronous twin, closed by
    /// <see href="https://github.com/karlssberg/Motiv/issues/204">#204</see>. It held a size local of
    /// its own until then, because #202's carrier is a thread-static and a continuation may resume on
    /// a thread whose slot holds a suspended evaluation's count — so the asynchronous pair admitted
    /// what the synchronous pair refused.
    /// </summary>
    [Fact]
    public async Task Should_bound_a_decorator_layered_async_evaluation()
    {
        MotivLimits.MaxEvaluationSize = 100;

        var spec = AsyncNestedChain(layers: 50, operandsPerLayer: 10);

        await Should.ThrowAsync<SpecException>(async () => await spec.EvaluateAsync(2));
    }

    /// <summary>The same hole on the asynchronous allocation-free path.</summary>
    [Fact]
    public async Task Should_bound_a_decorator_layered_async_match()
    {
        MotivLimits.MaxEvaluationSize = 100;

        var spec = AsyncNestedChain(layers: 50, operandsPerLayer: 10);

        await Should.ThrowAsync<SpecException>(async () => await spec.MatchesAsync(2));
    }

    private static SpecBase<int, string> Leaf(int index) =>
        Spec.Build((int n) => n % 2 == 0).Create($"p{index} is even");

    private static SpecBase<int, string> FlatChain(int operands) =>
        Enumerable.Range(0, operands).Select(Leaf).Aggregate((left, right) => left.And(right));

    /// <summary>
    /// The alternating shape: an operator run, a decorator over it, another operator run over that.
    /// A rule document composes exactly this — <c>RuleBinder.Decorate</c> wraps every node carrying a
    /// <c>name</c> or a <c>whenTrue</c>.
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
    /// The asynchronous twin of <see cref="NestedChain" />. Written out rather than shared, because
    /// the point of the case it serves is that the asynchronous fold holds a size local of its own.
    /// </summary>
    private static AsyncSpecBase<int, string> AsyncNestedChain(int layers, int operandsPerLayer)
    {
        var spec = Leaf(0).ToAsyncSpec();
        for (var layer = 0; layer < layers; layer++)
        {
            var inner = spec;
            for (var operand = 0; operand < operandsPerLayer; operand++)
                inner = inner.And(Leaf(operand).ToAsyncSpec());
            spec = Spec.Build(inner).Create($"layer{layer}");
        }

        return spec;
    }
}
