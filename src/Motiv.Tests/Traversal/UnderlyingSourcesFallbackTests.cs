using Motiv.Traversal;

namespace Motiv.Tests.Traversal;

/// <summary>
/// Cover for ticket #188 — the question #136 deferred. All three source walks used to answer "what
/// are my sources?" with <i>myself</i> when nothing contributed. A leaf has nothing underlying it, so
/// the honest answer is nothing; these tests fix that, and pin the premise that makes the change
/// invisible to the library's own consumers.
/// </summary>
/// <remarks>
/// The change is wholesale by construction. #136 folded the three walks onto one helper,
/// <c>BooleanResultBase.SourcesOf</c>, precisely so that the fallback lived in one place; changing it
/// for one walk is no longer expressible.
/// </remarks>
public class UnderlyingSourcesFallbackTests
{
    private static SpecBase<string, string> Leaf(string name, bool value) =>
        Spec.Build((string _) => value).WhenTrue($"{name}-true").WhenFalse($"{name}-false").Create();

    [Fact]
    public void Should_report_no_sources_for_an_atomic_result()
    {
        var result = Leaf("a", true).Evaluate("model");

        result.UnderlyingAssertionSources.ShouldBeEmpty();
        result.UnderlyingAllAssertionSources.ShouldBeEmpty();
        result.UnderlyingMetadataSources.ShouldBeEmpty();
    }

    /// <summary>
    /// A higher-order result is not an operation result, and the ones built from a boolean predicate
    /// expose no causes at all — so they reach the same branch by a different route than an atomic
    /// proposition does, and must answer the same way.
    /// </summary>
    [Fact]
    public void Should_report_no_sources_for_a_higher_order_result_that_exposes_no_causes()
    {
        var result = Spec
            .Build((string value) => value.Length > 0)
            .AsAllSatisfied()
            .Create("all non-empty")
            .Evaluate(["a", "b"]);

        // All three, because the three walks are handed three different child sequences: Causes,
        // Underlying and CausesWithValues. Naming only one would leave two thirds of the premise
        // resting on the test passing.
        result.Causes.ShouldBeEmpty("the premise of this test is that this node has no children");
        result.Underlying.ShouldBeEmpty("the premise of this test is that this node has no children");
        result.CausesWithValues.ShouldBeEmpty("the premise of this test is that this node has no children");

        result.UnderlyingAssertionSources.ShouldBeEmpty();
        result.UnderlyingAllAssertionSources.ShouldBeEmpty();
        result.UnderlyingMetadataSources.ShouldBeEmpty();
    }

    /// <summary>
    /// The deliberate non-change. The ticket asked whether <c>RootValues</c>' own fall-back-to-self —
    /// <c>GetRootValues().ElseIfEmpty(Values)</c> — belongs in the same sweep. It does not, and this
    /// pins the distinction so a later tidy-up cannot quietly collapse the two.
    /// <para>
    /// <c>Underlying*Sources</c> names <i>other nodes</i>: answering it with the node itself is a
    /// category error against the word "underlying", and it is what makes a descent non-terminating.
    /// The <c>Root*</c> family projects <i>values</i> out of the leaves, and the values at a leaf
    /// genuinely are its own — the projection is total, and there is no descent to hang.
    /// </para>
    /// </summary>
    [Fact]
    public void Should_leave_the_root_value_projections_falling_back_to_their_own_values()
    {
        var result = Leaf("a", true).Evaluate("model");

        result.RootValues.ShouldBe(["a-true"]);
        result.RootAssertions.ShouldBe(["a-true"]);
        result.AllRootAssertions.ShouldBe(["a-true"]);
    }

    /// <summary>
    /// The trap the ticket names. A consumer writing a generic descent — "keep taking the first
    /// underlying source until there are none" — never exits at a leaf while the leaf names itself,
    /// and the hang is invisible until it happens. The step bound turns that hang into a failure.
    /// </summary>
    [Theory]
    [MemberData(nameof(ResultTreeGenerator.SeedData), MemberType = typeof(ResultTreeGenerator))]
    public void Should_terminate_a_fixpoint_descent_at_every_node(int seed)
    {
        const int bound = 1_000;

        foreach (var node in ResultTreeGenerator.CorpusNodes(seed))
        {
            Descend<BooleanResultBase<string>>(node, result => result.UnderlyingMetadataSources, seed);
            Descend<BooleanResultBase>(node, result => result.UnderlyingAssertionSources, seed);
            Descend<BooleanResultBase>(node, result => result.UnderlyingAllAssertionSources, seed);
        }

        return;

        static void Descend<TResult>(
            TResult start,
            Func<TResult, IEnumerable<TResult>> sourcesOf,
            int seed)
            where TResult : BooleanResultBase
        {
            var current = start;

            for (var step = 0; step <= bound; step++)
            {
                var next = sourcesOf(current).FirstOrDefault();
                if (next is null)
                    return;

                current = next;
            }

            throw new ShouldAssertException(
                $"a descent through the underlying sources did not reach a node with none after " +
                $"{bound} steps, so a consumer walking to the leaves would hang (seed {seed})");
        }
    }

    /// <summary>
    /// The premise that makes dropping the fallback invisible to the library itself. Both in-library
    /// consumers — <c>Explanation.Resolve</c> and <c>MetadataNode.Resolve</c> — reach a source walk
    /// only behind an <see cref="IBooleanOperationResult" /> guard, so the fallback was reachable from
    /// inside the library only if some operation node had an empty causal set. None does:
    /// <c>GetCausalResults</c> is total for every operator (satisfied ⇒ some operand matched;
    /// unsatisfied ⇒ some operand did not). If that ever stops holding, this change becomes breaking
    /// and this test says so.
    /// </summary>
    [Theory]
    [MemberData(nameof(ResultTreeGenerator.SeedData), MemberType = typeof(ResultTreeGenerator))]
    public void Should_never_present_an_operation_node_with_an_empty_causal_set(int seed)
    {
        foreach (var node in OperationNodes(seed))
        {
            node.Causes.ShouldNotBeEmpty($"an operation always has a causal operand (seed {seed})");
            node.Underlying.ShouldNotBeEmpty($"an operation always has an operand (seed {seed})");
            node.CausesWithValues.ShouldNotBeEmpty(
                $"an operation always has a causal operand with values (seed {seed})");
        }
    }

    /// <summary>
    /// The "actually exercised" guard for the premise above, at corpus level rather than per seed —
    /// four of the hundred and fifty seeds generate a tree with no operation node in it at all, so a
    /// per-seed guard would fail on a corpus that is merely narrow rather than one that is unchecked.
    /// </summary>
    [Fact]
    public void Should_exercise_the_premise_across_the_corpus() =>
        Enumerable
            .Range(1, ResultTreeGenerator.SeedCount)
            .SelectMany(OperationNodes)
            .ShouldNotBeEmpty("the premise must actually have been exercised somewhere");

    /// <summary>
    /// "Wholesale" made checkable. #136 unified the three walks onto one helper precisely so that the
    /// fallback could not be changed for one of them alone; this asserts the consequence — the three
    /// report no sources at exactly the same nodes, never two of three. Over the corpus that is 6,109
    /// of 13,680 nodes, and the same 6,109 for each walk.
    /// </summary>
    [Theory]
    [MemberData(nameof(ResultTreeGenerator.SeedData), MemberType = typeof(ResultTreeGenerator))]
    public void Should_report_no_sources_at_the_same_nodes_for_all_three_walks(int seed)
    {
        var because =
            $"the three source walks are one algorithm, so none of them may keep a fallback the " +
            $"others have lost (seed {seed})";

        foreach (var node in ResultTreeGenerator.CorpusNodes(seed))
        {
            var assertionSourcesEmpty = !node.UnderlyingAssertionSources.Any();

            (!node.UnderlyingAllAssertionSources.Any()).ShouldBe(assertionSourcesEmpty, because);
            (!node.UnderlyingMetadataSources.Any()).ShouldBe(assertionSourcesEmpty, because);
        }
    }

    private static IEnumerable<BooleanResultBase<string>> OperationNodes(int seed) =>
        ResultTreeGenerator.CorpusNodes(seed).Where(node => node is IBooleanOperationResult);
}
