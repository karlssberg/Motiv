namespace Motiv.Tests.Traversal;

/// <summary>
/// <see cref="MotivLimits.MaxEvaluationSize" /> bounds one evaluation on the asynchronous surface too,
/// which <see href="https://github.com/karlssberg/Motiv/issues/204">#204</see> made true.
/// <see href="https://github.com/karlssberg/Motiv/issues/202">#202</see> reached only the synchronous
/// folds, because its carrier is a thread-static and an asynchronous continuation may resume on a
/// thread whose slot holds a <em>suspended</em> evaluation's count — so
/// <see cref="AsyncSpecBase{TModel}.EvaluateAsync" /> kept a fold-local size and admitted compositions
/// that <see cref="SpecBase{TModel}.Evaluate" /> refused.
/// </summary>
/// <remarks>
/// The asynchronous side has three shapes the synchronous side does not, and each is a case here:
/// <list type="number">
/// <item>a <b>concurrent operator</b>, which is a fan-out rather than a walk, so it is not folded and
/// its two branches run at once against one counter;</item>
/// <item>a <b>synchronous sub-composition</b> reached through <c>ToAsyncSpec()</c>, which runs a
/// synchronous fold inside an asynchronous evaluation — the seam where an asymmetry would otherwise
/// move rather than disappear;</item>
/// <item>a <b>suspended</b> evaluation, whose count must not be visible to whatever else runs on the
/// thread it left behind.</item>
/// </list>
/// </remarks>
[Collection(MotivLimitsTestCollection.Name)]
public class AsyncEvaluationBudgetTests : IDisposable
{
    private readonly int _previous = MotivLimits.MaxEvaluationSize;

    public void Dispose() => MotivLimits.MaxEvaluationSize = _previous;

    /// <summary>
    /// The arithmetic, stated exactly rather than by a comfortable margin, and matching
    /// <see cref="EvaluationBudgetTests.Should_admit_a_decorator_layered_composition_of_exactly_the_limit" />
    /// node for node: two decorator layers of one operand each is six nodes — per layer, the operation
    /// at the fold's root plus its two operands.
    /// </summary>
    [Fact]
    public async Task Should_admit_an_async_decorator_layered_composition_of_exactly_the_limit()
    {
        MotivLimits.MaxEvaluationSize = 6;

        (await NestedChain(layers: 2, operandsPerLayer: 1).EvaluateAsync(2)).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_abandon_an_async_decorator_layered_composition_one_node_past_the_limit()
    {
        MotivLimits.MaxEvaluationSize = 5;

        var spec = NestedChain(layers: 2, operandsPerLayer: 1);

        await Should.ThrowAsync<SpecException>(async () => await spec.EvaluateAsync(2));
    }

    /// <summary>
    /// A concurrent operator is the one asynchronous shape that is not folded at all: it evaluates its
    /// two operands through <see cref="Task.WhenAll(Task[])" /> and each branch enters a fold of its
    /// own. Nothing above them holds a budget, so without a carrier that both branches can reach, the
    /// bound applies per branch — the per-fold defect in a second place.
    /// </summary>
    /// <remarks>
    /// Fifteen nodes: seven in each branch (a left-deep chain of <c>n</c> leaves is <c>2n - 1</c>
    /// nodes), plus the concurrent node itself. The limit sits above either branch alone and below the
    /// pair, so a per-branch bound passes and a per-evaluation one refuses.
    /// </remarks>
    [Fact]
    public async Task Should_count_a_concurrent_operators_two_branches_against_one_budget()
    {
        MotivLimits.MaxEvaluationSize = 10;

        var spec = FlatChain(4).AndConcurrently(FlatChain(4));

        await Should.ThrowAsync<SpecException>(async () => await spec.EvaluateAsync(2));
    }

    /// <summary>The companion, so the case above cannot pass on a limit that was simply mean.</summary>
    [Fact]
    public async Task Should_admit_a_concurrent_composition_of_exactly_the_limit()
    {
        MotivLimits.MaxEvaluationSize = 15;

        var spec = FlatChain(4).AndConcurrently(FlatChain(4));

        (await spec.EvaluateAsync(2)).Satisfied.ShouldBeTrue();
    }

    /// <summary>
    /// And the node the fan-out itself contributes, which is the pair of cases above taken one apart.
    /// A concurrent node reached from a fold was charged by that fold as an operand; one that is the
    /// whole evaluation was charged by nothing, so the fan-out charges it — and only then. Without that
    /// the composition costs fourteen and this passes; with it, fifteen and it does not.
    /// </summary>
    [Fact]
    public async Task Should_charge_a_concurrent_node_that_is_the_whole_evaluation()
    {
        MotivLimits.MaxEvaluationSize = 14;

        var spec = FlatChain(4).AndConcurrently(FlatChain(4));

        await Should.ThrowAsync<SpecException>(async () => await spec.EvaluateAsync(2));
    }

    /// <summary>
    /// The other half of that claim: reached from a fold, the concurrent node is charged once and not
    /// twice. Thirteen nodes — the enclosing <c>And</c>, its leaf operand, the concurrent node it
    /// reaches, and five in each branch. Charged twice it is fourteen, and this limit refuses it.
    /// </summary>
    [Fact]
    public async Task Should_not_charge_a_nested_concurrent_node_twice()
    {
        MotivLimits.MaxEvaluationSize = 13;

        var spec = Leaf(0).And(FlatChain(3).AndConcurrently(FlatChain(3)));

        (await spec.EvaluateAsync(2)).Satisfied.ShouldBeTrue();
    }

    /// <summary>
    /// The seam the two carriers meet at. An asynchronous composition may contain a synchronous
    /// proposition through <c>ToAsyncSpec()</c>, and that adapter evaluates it through a synchronous
    /// fold. Were the synchronous fold to keep reading only its own thread-static, the asymmetry #204
    /// closes would not disappear — it would move here, and a caller could still spend twice the bound
    /// by putting half the composition behind an adapter.
    /// </summary>
    /// <remarks>
    /// Ten nodes: the asynchronous fold's root <c>And</c> and its two operands, then the adapter's
    /// seven-node synchronous chain beneath it. The adapter is charged as a node in its own right, as
    /// every decorator is.
    /// </remarks>
    [Fact]
    public async Task Should_count_a_synchronous_sub_composition_against_the_async_evaluations_budget()
    {
        MotivLimits.MaxEvaluationSize = 8;

        var spec = Leaf(0).And(SyncFlatChain(4).ToAsyncSpec());

        await Should.ThrowAsync<SpecException>(async () => await spec.EvaluateAsync(2));
    }

    [Fact]
    public async Task Should_admit_a_synchronous_sub_composition_of_exactly_the_limit()
    {
        MotivLimits.MaxEvaluationSize = 10;

        var spec = Leaf(0).And(SyncFlatChain(4).ToAsyncSpec());

        (await spec.EvaluateAsync(2)).Satisfied.ShouldBeTrue();
    }

    /// <summary>
    /// <b>A suspended evaluation's count must not be visible to whatever runs next on its thread.</b>
    /// This is the whole reason the asynchronous carrier could not be the thread-static #202 used, and
    /// it is the one claim here that no arithmetic case can make: a fold-local size is per-fold and
    /// wrong, a thread-static is per-thread and wrong, and both bound a single unsuspended evaluation
    /// identically.
    /// </summary>
    /// <remarks>
    /// The interleaving is chosen rather than raced. The gated leaf suspends the first evaluation
    /// having charged exactly two nodes — its fold's root and the gate operand — and everything in the
    /// second evaluation completes synchronously, so it runs to completion on the thread the first one
    /// left. The limit admits either alone (nine nodes and seven) and refuses their sum, so a carrier
    /// the two evaluations share turns this red at the point the first one resumes.
    /// </remarks>
    [Fact]
    public async Task Should_not_charge_a_suspended_evaluation_to_one_running_on_the_same_thread()
    {
        MotivLimits.MaxEvaluationSize = 10;

        var gate = new TaskCompletionSource<bool>();
        var suspends = Spec.BuildAsync((int _) => new ValueTask<bool>(gate.Task)).Create("gate");

        var pending = suspends.And(FlatChain(4)).EvaluateAsync(2);

        (await FlatChain(4).EvaluateAsync(2)).Satisfied.ShouldBeTrue(
            "the second evaluation is within the bound on its own account");

        gate.SetResult(true);

        (await pending).Satisfied.ShouldBeTrue(
            "and the first must resume with only its own spending charged to it");
    }

    /// <summary>
    /// A budget belongs to one evaluation. Were it merely established at the top and never released,
    /// the second call would inherit the first's spending and fail at half the composition — and on
    /// this surface the release is not ours to write: an asynchronous method's synchronous prefix
    /// writes to the caller's execution context, and the state-machine builder restores it on return.
    /// This is what pins that.
    /// </summary>
    [Fact]
    public async Task Should_start_each_async_evaluation_with_its_whole_budget()
    {
        MotivLimits.MaxEvaluationSize = 6;

        var spec = NestedChain(layers: 2, operandsPerLayer: 1);

        (await spec.EvaluateAsync(2)).Satisfied.ShouldBeTrue();
        (await spec.EvaluateAsync(2)).Satisfied.ShouldBeTrue();
        (await spec.MatchesAsync(2)).ShouldBeTrue();
    }

    /// <summary>
    /// And is released when the evaluation is abandoned, not only when it completes. An unwound budget
    /// left behind would make the next caller's ordinary composition fail — the failure landing
    /// somewhere other than the fault.
    /// </summary>
    [Fact]
    public async Task Should_release_the_budget_when_an_async_evaluation_is_abandoned()
    {
        MotivLimits.MaxEvaluationSize = 5;

        var oversized = NestedChain(layers: 2, operandsPerLayer: 1);
        await Should.ThrowAsync<SpecException>(async () => await oversized.EvaluateAsync(2));

        var within = NestedChain(layers: 1, operandsPerLayer: 1);
        (await within.EvaluateAsync(2)).Satisfied.ShouldBeTrue();
    }

    /// <summary>
    /// The end that pulls the other way, restated on this surface: work done <em>inside</em> a node
    /// stays uncounted. A <c>Tap</c> callback is a side effect hung off a node rather than part of the
    /// decision the node makes, and making the budget reachable from an asynchronous fold must not
    /// start charging it.
    /// </summary>
    [Fact]
    public async Task Should_not_charge_a_tap_callbacks_own_evaluation_to_an_async_budget()
    {
        MotivLimits.MaxEvaluationSize = 20;

        var audit = SyncFlatChain(10); // 19 nodes — within the limit alone, over it when added
        var tapped = Spec.Build((int n) => n % 2 == 0).Create("tapped is even")
            .Tap((int model, BooleanResultBase<string> _) => audit.Evaluate(model));
        var spec = Leaf(0).And(tapped.ToAsyncSpec());

        (await spec.EvaluateAsync(2)).Satisfied.ShouldBeTrue();
    }

    private static AsyncSpecBase<int, string> Leaf(int index) =>
        Spec.BuildAsync((int n) => new ValueTask<bool>(n % 2 == 0)).Create($"p{index} is even");

    private static AsyncSpecBase<int, string> FlatChain(int operands) =>
        Enumerable.Range(0, operands).Select(Leaf).Aggregate((left, right) => left.And(right));

    private static SpecBase<int, string> SyncFlatChain(int operands) =>
        Enumerable.Range(0, operands)
            .Select(index => Spec.Build((int n) => n % 2 == 0).Create($"s{index} is even"))
            .Aggregate((left, right) => left.And(right));

    /// <summary>
    /// The alternating shape — an operator run, a decorator over it, another operator run over that —
    /// composed from genuinely asynchronous leaves so that the node count matches its synchronous twin
    /// exactly rather than picking up an adapter per leaf.
    /// </summary>
    private static AsyncSpecBase<int, string> NestedChain(int layers, int operandsPerLayer)
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
}
