using Motiv.Traversal;
using static Motiv.Tests.Traversal.OracleHelpers;

namespace Motiv.Tests.Traversal;

/// <summary>
/// Cover for ticket #136 — the question Spec 3A deferred. The three source walks are one algorithm;
/// <see cref="BooleanResultBase{TMetadata}.UnderlyingMetadataSources" /> had drifted from its two
/// assertion-source siblings in two ways, both of which are covered here: it yielded the result
/// <i>itself</i> once per non-operation child rather than the child the walk stopped at, and it had
/// no fallback for a result that contributed nothing. That fallback was then removed from all three
/// walks by ticket #188 — see <see cref="UnderlyingSourcesFallbackTests" />.
/// </summary>
/// <remarks>
/// The last test covers the consequence the ticket did not name. `MetadataNode.Resolve` is the walk's
/// only in-library consumer, so correcting it also corrects the metadata tier tree — and with it the
/// public <see cref="BooleanResultBase{TMetadata}.RootValues" />, which was silently dropping the
/// values of contributing operands. Nothing in the suite covered that before, which is how it shipped.
/// </remarks>
public class UnderlyingMetadataSourcesTests
{
    [Fact]
    public void Should_yield_the_child_the_walk_stopped_at_rather_than_the_result_itself()
    {
        var result = Leaf("a", true).And(Leaf("b", true)).And(Leaf("c", true)).Evaluate("model");

        var sources = result.UnderlyingMetadataSources.ToArray();

        sources.Select(source => source.Reason).ShouldBe(["a-true", "b-true", "c-true"]);
        sources.ShouldNotContain(
            source => ReferenceEquals(source, result),
            "the name of this property promises the operands, not the composition they came from");
    }

    [Theory]
    [MemberData(nameof(ResultTreeGenerator.SeedData), MemberType = typeof(ResultTreeGenerator))]
    public void Should_never_report_an_operation_result_as_a_source(int seed)
    {
        var examined = 0;

        // Every node qualifies since #188. While the walk still fell back to itself, a node with no
        // causal values was its own source, so the claim had to be narrowed to exclude such nodes in
        // case one was ever an operation result.
        foreach (var node in ResultTreeGenerator.CorpusNodes(seed))
        {
            examined++;
            node.UnderlyingMetadataSources.ShouldNotContain(
                source => source is IBooleanOperationResult,
                $"the walk descends through operations and stops at the operands that produced the " +
                $"values, so an operation node is never itself a source (seed {seed})");
        }

        examined.ShouldBeGreaterThan(0, "the invariant must actually have been exercised");
    }

    [Theory]
    [MemberData(nameof(ResultTreeGenerator.SeedData), MemberType = typeof(ResultTreeGenerator))]
    public void Should_agree_with_its_assertion_source_sibling_wherever_the_causal_sets_agree(int seed)
    {
        var comparer = ReferenceComparer<BooleanResultBase>.Instance;
        var compared = 0;

        foreach (var node in ResultTreeGenerator.CorpusNodes(seed))
        {
            if (!node.Causes.SequenceEqual(node.CausesWithValues, comparer))
                continue;

            compared++;
            node.UnderlyingMetadataSources
                .SequenceEqual(node.UnderlyingAssertionSources, comparer)
                .ShouldBeTrue(
                    $"the metadata-source walk and the assertion-source walk are the same algorithm, " +
                    $"so they cannot disagree on a node whose two causal sets are the same (seed {seed})");
        }

        compared.ShouldBeGreaterThan(0, "the invariant must actually have been exercised");
    }

    [Theory]
    [MemberData(nameof(ResultTreeGenerator.SeedData), MemberType = typeof(ResultTreeGenerator))]
    public void Should_reach_every_causal_leaf_from_RootValues(int seed)
    {
        foreach (var node in ResultTreeGenerator.CorpusNodes(seed))
            node.RootValues.ShouldBe(
                DistinctInOrder(CausalLeafValues(node)),
                $"RootValues is the metadata of every result that evaluated, so it must reach the " +
                $"causal leaves — descending CausesWithValues independently reaches the same set " +
                $"(seed {seed})");
    }

    /// <summary>
    /// The invariant above once excluded higher-order subtrees — issue #189, the residue #136's fix
    /// did not reach. The exclusion is gone, so the exclusion's own premise has to be asserted
    /// separately: a corpus that stopped generating higher-order results would leave the invariant
    /// green while covering none of what #189 was about.
    /// </summary>
    [Fact]
    public void Should_exercise_the_invariant_over_higher_order_subtrees() =>
        Enumerable
            .Range(1, ResultTreeGenerator.SeedCount)
            .SelectMany(ResultTreeGenerator.CorpusNodes)
            .Where(ContainsHigherOrder)
            .ShouldNotBeEmpty(
                "the corpus must still reach higher-order results, or the RootValues invariant above " +
                "is no longer covering the case #189 was about");

    /// <summary>
    /// The corner the corpus cannot reach: a branch that has children but whose whole subtree yields
    /// no metadata. The walk has to fall back when a branch <i>contributed nothing</i>, not merely
    /// when it <i>had no children</i> — otherwise such a branch drops its own value, which is #189
    /// one level up. Its assertion twin, <c>CombineRootAssertions</c>, has always fallen back this
    /// way; only a proposition yielding an empty sequence tells the two forms apart.
    /// </summary>
    [Fact]
    public void Should_fall_back_for_a_branch_whose_subtree_yields_no_values()
    {
        var yieldsNothing = Spec
            .Build((string _) => true)
            .WhenTrueYield(_ => Enumerable.Empty<string>())
            .WhenFalseYield(_ => Enumerable.Empty<string>())
            .Create("yields nothing");

        var allSatisfied = Spec
            .Build(yieldsNothing)
            .AsAllSatisfied()
            .WhenTrue("all yielded nothing")
            .WhenFalse("not all yielded nothing")
            .Create();

        var result = allSatisfied.Evaluate(["a", "b"]).And(Leaf("sibling", true).Evaluate("model"));

        result.RootValues.ShouldBe(
            ["all yielded nothing", "sibling-true"],
            "the higher-order branch carries its own value and its operands carry none, so falling " +
            "back only for a childless branch would drop it");
    }

    /// <summary>
    /// An independent formulation of "the values of every causal leaf", owing nothing to
    /// <see cref="MetadataNode{TMetadata}" />. Before #136 the tier tree was built from a walk that
    /// returned ancestors, so <c>RootValues</c> disagreed with this on 566 of the corpus's 13,680
    /// nodes — dropping values every time, never inventing one.
    /// </summary>
    private static IEnumerable<string> CausalLeafValues(BooleanResultBase<string> result) =>
        result.CausesWithValues.Any()
            ? result.CausesWithValues.SelectMany(CausalLeafValues)
            : result.Values;
}
