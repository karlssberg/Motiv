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
    /// Stated over both funnels and all three iteration shapes, because neither axis is free. The
    /// materializing funnel and the allocation-free <c>HigherOrderShortCircuit</c> reach their elements
    /// by different routes, so excluding one would leave the other charging; and each funnel takes a
    /// <c>T[]</c> fast path, an <see cref="IReadOnlyList{T}" /> path and a <c>foreach</c> fallback, so a
    /// single shape leaves two branches unpinned.
    /// </para>
    /// <para>
    /// The higher-order proposition is composed into an operation deliberately: evaluated on its own it
    /// is the outermost fold's caller, so every element would start a fresh budget whether the
    /// exclusion existed or not, and the case would pass without proving anything.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Unwrapping the array branch of either funnel left the whole suite green while this stood as a
    /// pair of single-shape cases over <see cref="Enumerable.Repeat{TResult}" />, which is never an
    /// array. A caller handing over an <c>Order[]</c> — the shape <see cref="HigherOrderResults" />' own
    /// remarks call the one the hot path supplies — would have lost the exclusion with nothing going
    /// red.
    /// </remarks>
    [Theory]
    [MemberData(nameof(ElementSources))]
    public void Should_not_charge_a_higher_order_propositions_per_element_work_to_the_budget(
        string shape,
        Func<IEnumerable<int>> source)
    {
        MotivLimits.MaxEvaluationSize = 100;

        // 50 elements, three nodes apiece — 150 in all, against a limit of 100.
        var composed = AllElementsPass(elements: 50).And(NonEmpty());

        composed.Evaluate(source()).Satisfied.ShouldBeTrue($"as a {shape}, on the materializing funnel");
        composed.Matches(source()).ShouldBeTrue($"as a {shape}, on the short-circuiting funnel");
    }

    public static TheoryData<string, Func<IEnumerable<int>>> ElementSources() =>
        new()
        {
            { "T[]", () => Enumerable.Repeat(2, 50).ToArray() },
            { "List<T>", () => Enumerable.Repeat(2, 50).ToList() },
            { "lazy sequence", () => Enumerable.Range(0, 50).Select(_ => 2) }
        };

    /// <summary>
    /// Producing an element is part of resolving it. Only the projection was excluded, so a lazy source
    /// whose <c>MoveNext</c> evaluates a proposition charged the composition once per element — and the
    /// same models passed as an array did not, because an array is fully produced before the funnel is
    /// entered. A bound that depends on whether the caller wrote <c>.ToArray()</c> is not a bound.
    /// </summary>
    [Fact]
    public void Should_not_charge_a_lazy_sources_enumeration_to_the_budget()
    {
        MotivLimits.MaxEvaluationSize = 100;

        var perElement = Leaf(0).And(Leaf(1)).And(Leaf(2)); // five nodes for every MoveNext
        var composed = AllElements(Leaf(9)).And(NonEmpty());

        IEnumerable<int> Lazy() => Enumerable.Range(0, 100).Select(_ => 2).Where(perElement.Matches);

        composed.Evaluate(Lazy()).Satisfied.ShouldBeTrue();
        composed.Matches(Lazy()).ShouldBeTrue();
    }

    /// <summary>
    /// <see cref="EnumerableExtensions.Where{TModel,TMetadata}" /> is the library's third place that
    /// resolves one element at a time, and it was not excluded — which made
    /// <c>EvaluationBudget</c>'s claim that there were only two of them false in its own assembly.
    /// </summary>
    [Fact]
    public void Should_not_charge_the_where_extensions_per_element_evaluation()
    {
        MotivLimits.MaxEvaluationSize = 100;

        var isPriority = Leaf(0).And(Leaf(1)); // three nodes per element
        var anyPriority = Spec
            .Build((IEnumerable<int> models) => models.Where(isPriority).Any())
            .Create("any is priority");

        var composed = anyPriority.And(NonEmpty());

        composed.Evaluate(Enumerable.Repeat(2, 50).ToArray()).Satisfied.ShouldBeTrue();
    }

    /// <summary>
    /// A <c>Tap</c> callback is a side effect hung off a node, not part of the decision the node makes,
    /// so whatever it evaluates is work inside that node. Charged, adding an audit hook to a rule could
    /// make the rule itself refuse — the failure attributed to the decision rather than to the
    /// observability that caused it.
    /// </summary>
    [Fact]
    public void Should_not_charge_a_tap_callbacks_own_evaluation_to_the_budget()
    {
        MotivLimits.MaxEvaluationSize = 20;

        var audit = FlatChain(10); // 19 nodes — within the limit alone, over it when added
        var composed = Leaf(0).Tap((model, _) => audit.Evaluate(model)).And(Leaf(1));

        composed.Evaluate(2).Satisfied.ShouldBeTrue();
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
    /// The half of the exclusion a leak canary cannot see. <c>EvaluationBudget.Exclude</c> <em>parks</em> the count
    /// and hands it back; it does not discard it. A version that zeroed the count and never restored
    /// would leak in the <em>permissive</em> direction — the composition forgetting what it spent before
    /// the higher-order operand — so the bound would quietly weaken rather than misfire.
    /// </summary>
    /// <remarks>
    /// Nothing else here catches that. Every exclusion case asserts an evaluation <i>succeeds</i>, and
    /// <see cref="Should_leave_no_budget_behind_however_an_evaluation_fails" /> looks for spending left
    /// <i>behind</i> — a discarded count leaves none. It was found by deleting the restore and watching
    /// the suite stay green.
    /// </remarks>
    [Fact]
    public void Should_resume_the_compositions_count_after_a_higher_order_operand()
    {
        MotivLimits.MaxEvaluationSize = 7;

        // Seven nodes: four operands and the three operations joining them. Only the first is
        // higher-order, and its element is a plain leaf, so no nested fold runs beneath the suppression
        // — were the count discarded rather than parked, the three operands after it would be counted
        // from zero and this would cost three.
        var composed = HigherOrderThenChain();

        composed.Evaluate(Enumerable.Repeat(2, 4)).Satisfied.ShouldBeTrue();

        MotivLimits.MaxEvaluationSize = 6;

        var act = () => HigherOrderThenChain().Evaluate(Enumerable.Repeat(2, 4));

        act.ShouldThrow<SpecException>();
    }

    /// <summary>The same, on the funnel <c>Matches</c> takes.</summary>
    [Fact]
    public void Should_resume_the_compositions_count_after_a_higher_order_operand_on_matches()
    {
        MotivLimits.MaxEvaluationSize = 6;

        var act = () => { _ = HigherOrderThenChain().Matches(Enumerable.Repeat(2, 4)); };

        act.ShouldThrow<SpecException>();
    }

    /// <summary>
    /// The leak invariant, over every way an evaluation can end badly: <b>however an evaluation
    /// terminates, the thread's count is back to zero.</b>
    /// </summary>
    /// <remarks>
    /// Ambient state's failure mode is that a leak surfaces somewhere other than the fault — the next
    /// caller on the thread gets an unexplained refusal, and nothing points back here. That makes the
    /// exceptional paths the ones worth enumerating rather than reasoning about, because reasoning about
    /// them is exactly what a later edit will get wrong.
    /// <para>
    /// Checked black-box, with no test hook into the budget: <see cref="Canary" /> costs precisely the
    /// limit, so it is satisfied only from a count of zero and refused by a leak of even one node. That
    /// keeps the invariant stated in terms of behaviour a caller can see, rather than in terms of a
    /// private field a refactor is free to rename.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(FailureShapes))]
    public void Should_leave_no_budget_behind_however_an_evaluation_fails(string shape, Action provoke)
    {
        MotivLimits.MaxEvaluationSize = CanaryCost;

        Should.Throw<Exception>(provoke, $"the {shape} case must actually fail, or it proves nothing");

        Canary().Evaluate(2).Satisfied.ShouldBeTrue(
            $"a composition costing exactly the limit must still be admitted after a {shape} failure; " +
            "if it is refused, that failure left its spending on the thread");
        Canary().Matches(2).ShouldBeTrue($"and on the allocation-free path after a {shape} failure");
    }

    public static TheoryData<string, Action> FailureShapes() =>
        new()
        {
            // The outermost fold refuses. The only shape covered before this theory existed.
            { "bound-exceeded", () => FlatChain(CanaryCost * 4).Evaluate(2) },

            // The refusal is raised inside a *nested* fold, so it unwinds through an Ownership that
            // must NOT release, out through one that must.
            { "bound-exceeded-in-a-nested-fold", () => NestedChain(layers: 20, operandsPerLayer: 2).Evaluate(2) },

            // An arbitrary user exception mid-fold — not the budget's own, so nothing in the budget is
            // watching for it.
            { "throwing-predicate", () => Leaf(0).And(Throwing()).Evaluate(2) },
            { "throwing-predicate-on-matches", () => { _ = Leaf(0).And(Throwing()).Matches(2); } },

            // Thrown from inside the exclusion scope, so the restore has to happen on the exceptional
            // path too — and then the outer fold's release on top of it.
            { "throwing-element", () => AllElements(Throwing()).And(NonEmpty()).Evaluate(Enumerable.Repeat(2, 4)) },
            { "throwing-element-on-matches", () => { _ = AllElements(Throwing()).And(NonEmpty()).Matches(Enumerable.Repeat(2, 4)); } },

            // An element whose own composition exceeds the bound: a refusal raised beneath a suppression,
            // which the composition above it was never spending.
            { "oversized-element", () => AllElements(FlatChain(CanaryCost * 4)).And(NonEmpty()).Evaluate(Enumerable.Repeat(2, 2)) },

            // Thrown by the sequence itself — inside the exclusion scope, but not inside a projection.
            { "throwing-sequence", () => AllElements(Leaf(0)).And(NonEmpty()).Evaluate(ThrowingSequence()) }
        };

    /// <summary>
    /// A chain of six propositions — <c>2n - 1</c> nodes, so exactly <see cref="CanaryCost" />. Sized to
    /// the limit deliberately: a composition with any headroom would survive a small leak and the
    /// invariant would only be half-checked.
    /// </summary>
    private const int CanaryCost = 11;

    private static SpecBase<int, string> Canary() => FlatChain(6);

    private static SpecBase<int, string> Throwing() =>
        Spec.Build((int _) => throw new InvalidOperationException("thrown from inside an evaluation"))
            .Create("throws");

    private static SpecBase<IEnumerable<int>, string> AllElements(SpecBase<int, string> element) =>
        Spec.Build(element).AsAllSatisfied().Create("every element holds");

    private static IEnumerable<int> ThrowingSequence()
    {
        yield return 2;
        throw new InvalidOperationException("thrown while enumerating the models");
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

    /// <summary>
    /// A higher-order operand followed by three ordinary ones, all over the same collection model, so
    /// that the composition's count has to survive the suppression in the middle of it.
    /// </summary>
    private static SpecBase<IEnumerable<int>, string> HigherOrderThenChain() =>
        AllElements(Leaf(0))
            .And(CollectionLeaf(1))
            .And(CollectionLeaf(2))
            .And(CollectionLeaf(3));

    private static SpecBase<IEnumerable<int>, string> CollectionLeaf(int index) =>
        Spec.Build((IEnumerable<int> models) => models.Any()).Create($"c{index} is not empty");

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
