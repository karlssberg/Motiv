namespace Motiv.Tests.Traversal;

/// <summary>
/// <see cref="MotivLimits.MaxEvaluationSize" /> bounds one evaluation rather than one fold, which is
/// what it always documented and what
/// <see href="https://github.com/karlssberg/Motiv/issues/202">#202</see> made true. Two things have to
/// hold at once, and they pull in opposite directions:
/// <list type="number">
/// <item>A decorator's operand is part of the same logical composition, so a nested fold spends the
/// caller's budget — <see cref="DecoratorSeamTests" /> holds that end.</item>
/// <item>Work done <em>inside</em> a node is not, and a higher-order proposition over a large
/// collection must stay uncounted, which the same remarks have always promised. That is the end these
/// cases hold, because over-charging it would be a breaking change wearing a bug fix's clothes.</item>
/// </list>
/// </summary>
[Collection(MotivLimitsTestCollection.Name)]
public class EvaluationBudgetTests : IDisposable
{
    private readonly int _previous = MotivLimits.MaxEvaluationSize;

    public void Dispose() => MotivLimits.MaxEvaluationSize = _previous;

    /// <summary>
    /// The arithmetic, stated exactly rather than by a comfortable margin. Two decorator layers of one
    /// operand each is six nodes: per layer, the operation at the fold's root plus its two operands.
    /// A margin would pass whether the nested folds were charged once, twice, or by their whole
    /// subtree.
    /// </summary>
    [Fact]
    public void Should_admit_a_decorator_layered_composition_of_exactly_the_limit()
    {
        MotivLimits.MaxEvaluationSize = 6;

        NestedChain(layers: 2, operandsPerLayer: 1).Evaluate(2).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_abandon_a_decorator_layered_composition_one_node_past_the_limit()
    {
        MotivLimits.MaxEvaluationSize = 5;

        var act = () => NestedChain(layers: 2, operandsPerLayer: 1).Evaluate(2);

        act.ShouldThrow<SpecException>();
    }

    /// <summary>
    /// The documented exclusion. A higher-order proposition evaluates its inner spec once per element
    /// through the same entry point a decorator uses, so a budget that simply spanned everything would
    /// charge a 250,000-element collection 250,000 times over — and the remarks on
    /// <see cref="MotivLimits.MaxEvaluationSize" /> promise it does not.
    /// <para>
    /// The higher-order proposition is composed into an operation deliberately: evaluated on its own it
    /// is the outermost fold's caller, so every element would start a fresh budget whether the
    /// exclusion existed or not, and the case would pass without proving anything.
    /// </para>
    /// </summary>
    [Fact]
    public void Should_not_charge_a_higher_order_propositions_per_element_work_to_the_budget()
    {
        MotivLimits.MaxEvaluationSize = 100;

        // 50 elements, three nodes apiece — 150 in all, against a limit of 100.
        var composed = AllElementsPass(elements: 50).And(NonEmpty());

        composed.Evaluate(Enumerable.Repeat(2, 50)).Satisfied.ShouldBeTrue();
    }

    /// <summary>
    /// The same exclusion on the allocation-free path, which reaches its elements through
    /// <c>HigherOrderShortCircuit</c> rather than through the materializing helper — a different funnel,
    /// so suppressing one would leave the other charging.
    /// </summary>
    [Fact]
    public void Should_not_charge_a_higher_order_propositions_per_element_work_to_a_match()
    {
        MotivLimits.MaxEvaluationSize = 100;

        var composed = AllElementsPass(elements: 50).And(NonEmpty());

        composed.Matches(Enumerable.Repeat(2, 50)).ShouldBeTrue();
    }

    /// <summary>
    /// A budget belongs to one evaluation. Were it merely reset at the top and never released, the
    /// second call would inherit the first's spending and fail at half the composition.
    /// </summary>
    [Fact]
    public void Should_start_each_evaluation_with_its_whole_budget()
    {
        MotivLimits.MaxEvaluationSize = 6;

        var spec = NestedChain(layers: 2, operandsPerLayer: 1);

        spec.Evaluate(2).Satisfied.ShouldBeTrue();
        spec.Evaluate(2).Satisfied.ShouldBeTrue();
        spec.Matches(2).ShouldBeTrue();
    }

    /// <summary>
    /// And is released when the evaluation is abandoned, not only when it completes. An unwound budget
    /// left behind would make the *next* caller's ordinary composition fail — the failure landing
    /// somewhere other than the fault, which is the worst shape this bug could take.
    /// </summary>
    [Fact]
    public void Should_release_the_budget_when_an_evaluation_is_abandoned()
    {
        MotivLimits.MaxEvaluationSize = 5;

        var act = () => NestedChain(layers: 2, operandsPerLayer: 1).Evaluate(2);
        act.ShouldThrow<SpecException>();

        NestedChain(layers: 1, operandsPerLayer: 1).Evaluate(2).Satisfied.ShouldBeTrue();
    }

    /// <summary>
    /// Two threads are two evaluations. The synchronous folds never leave the thread that started them,
    /// which is what lets the budget be a thread-static at all; this states the property that permission
    /// rests on.
    /// </summary>
    [Fact]
    public void Should_budget_each_thread_independently()
    {
        MotivLimits.MaxEvaluationSize = 6;

        var spec = NestedChain(layers: 2, operandsPerLayer: 1);
        var outcomes = new bool[8];

        Parallel.For(0, outcomes.Length, i => outcomes[i] = spec.Evaluate(2).Satisfied);

        outcomes.ShouldAllBe(satisfied => satisfied);
    }

#if !NETFRAMEWORK

    /// <summary>
    /// The budget costs nothing to carry. <see cref="SpecBase{TModel}.Matches" /> allocates nothing —
    /// a contract Spec 3E paid for with a per-thread frame buffer, and which three sets of remarks now
    /// assert on this type's behalf while nothing checked it. An ambient budget is exactly the kind of
    /// change that would quietly end it, so the claim gets a gate rather than a paragraph.
    /// </summary>
    /// <remarks>
    /// Not built for <c>net472</c>, which has no per-thread allocation counter — the same reason
    /// <see cref="ReasonCostTests" /> gives for its own <c>#if</c>.
    /// </remarks>
    [Fact]
    public void Should_carry_the_budget_without_allocating()
    {
        var spec = FlatChain(operands: 16);

        _ = spec.Matches(2); // warm: the first call JITs the fold and fills the thread's frame buffer

        var before = GC.GetAllocatedBytesForCurrentThread();
        _ = spec.Matches(2);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBe(
            0,
            "a budget held in a thread-static and scoped by ref structs allocates nothing; a carrier " +
            "that boxed, closed over state, or copied an ExecutionContext would show up here");
    }

#endif

    private static SpecBase<int, string> Leaf(int index) =>
        Spec.Build((int n) => n % 2 == 0).Create($"p{index} is even");

    private static SpecBase<int, string> FlatChain(int operands) =>
        Enumerable.Range(0, operands).Select(Leaf).Aggregate((left, right) => left.And(right));

    /// <summary>
    /// An "all satisfied" proposition whose <em>element</em> spec is a composition rather than a leaf,
    /// so that each element costs the fold three nodes and the per-element work is worth excluding.
    /// </summary>
    private static SpecBase<IEnumerable<int>, string> AllElementsPass(int elements) =>
        Spec.Build(Leaf(0).And(Leaf(1)))
            .AsAllSatisfied()
            .Create($"all {elements} elements are even");

    private static SpecBase<IEnumerable<int>, string> NonEmpty() =>
        Spec.Build((IEnumerable<int> models) => models.Any()).Create("the collection is not empty");

    /// <summary>
    /// The alternating shape <see cref="DecoratorSeamTests" /> uses, restated here so a change to one
    /// suite's fixture cannot silently move the other's arithmetic.
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
}
