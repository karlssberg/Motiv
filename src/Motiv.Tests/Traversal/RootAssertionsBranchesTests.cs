using static Motiv.Tests.Traversal.OracleHelpers;

namespace Motiv.Tests.Traversal;

/// <summary>
/// Cover for ticket #192 — ticket #189 in the assertion tree. <c>Explanation</c> and
/// <see cref="MetadataNode{TMetadata}" /> are the same tree with the same <c>Resolution</c>, the same
/// collapse rule and the same <c>Underlying</c>, so the walk that could not ask a level for a
/// per-branch answer was wrong in both. #189 fixed the metadata half and measured the assertion half
/// at 84 disagreeing nodes.
/// <para>
/// Only the first invariant closes #192. The <c>AllRootAssertions</c> one refutes the ticket's other
/// half — that walk never had the defect — and is kept as the guard that keeps the absence true.
/// </para>
/// </summary>
public class RootAssertionsBranchesTests
{
    [Theory]
    [MemberData(nameof(ResultTreeGenerator.SeedData), MemberType = typeof(ResultTreeGenerator))]
    public void Should_reach_every_causal_leaf_from_RootAssertions(int seed)
    {
        foreach (var node in ResultTreeGenerator.CorpusNodes(seed))
            node.RootAssertions.ShouldBe(
                DistinctInOrder(CausalLeafAssertions(node)),
                $"RootAssertions is the assertions of every result that determined the outcome, so it " +
                $"must reach the causal leaves — descending Causes independently reaches the same set " +
                $"(seed {seed})");
    }

    [Theory]
    [MemberData(nameof(ResultTreeGenerator.SeedData), MemberType = typeof(ResultTreeGenerator))]
    public void Should_reach_every_leaf_from_AllRootAssertions(int seed)
    {
        foreach (var node in ResultTreeGenerator.CorpusNodes(seed))
            node.AllRootAssertions.ShouldBe(
                DistinctInOrder(LeafAssertions(node)),
                $"AllRootAssertions is the assertions of every result that evaluated, so it must reach " +
                $"the leaves of Underlying rather than only those of Causes (seed {seed})");
    }

    /// <summary>
    /// The first invariant is about higher-order subtrees — the only shape that makes one branch
    /// deeper than its siblings, and so the only one where a sibling's collapse is visible. A corpus
    /// that stopped generating them would leave it green while covering none of what #192 was about.
    /// </summary>
    [Fact]
    public void Should_exercise_the_invariant_over_higher_order_subtrees() =>
        Enumerable
            .Range(1, ResultTreeGenerator.SeedCount)
            .SelectMany(ResultTreeGenerator.CorpusNodes)
            .Where(ContainsHigherOrder)
            .ShouldNotBeEmpty(
                "the corpus must still reach higher-order results, or the RootAssertions invariant " +
                "above is no longer covering the case #192 was about");

    /// <summary>
    /// #189's review round found a corner in the metadata tier the corpus cannot reach — a branch
    /// with children whose whole subtree yields nothing — and made the fold fall back for a branch
    /// that <i>contributed nothing</i> rather than one that <i>had no children</i>. That corner does
    /// not exist here, and this pins why: a proposition's assertion has a total fallback that its
    /// metadata does not. <c>Create("yields nothing")</c> makes the yielded strings <c>Values</c> and
    /// the name plus its <c>== true</c> suffix the assertion, so an operand that yields nothing still
    /// asserts something. The two twins therefore give different answers for the same tree, and
    /// correctly so.
    /// </summary>
    [Fact]
    public void Should_report_a_yielding_operand_that_yielded_nothing_where_its_metadata_twin_cannot()
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

        result.RootAssertions.ShouldBe(
            ["yields nothing == true", "sibling-true"],
            "the operand yielded no strings but still asserts its name, so the branch contributed " +
            "and the higher-order level above it is not the deepest one");

        result.RootValues.ShouldBe(
            ["all yielded nothing", "sibling-true"],
            "the same operand contributed no metadata, so there the higher-order level is the " +
            "deepest — the asymmetry is the suffix rule, not a divergence between the two walks");
    }

    /// <summary>
    /// An independent formulation of "the assertions of every causal leaf", owing nothing to
    /// <c>Explanation</c>. This is the assertion twin of <c>CausalLeafValues</c> in
    /// <see cref="UnderlyingMetadataSourcesTests" />.
    /// </summary>
    private static IEnumerable<string> CausalLeafAssertions(BooleanResultBase result) =>
        result.Causes.Any()
            ? result.Causes.SelectMany(CausalLeafAssertions)
            : result.Assertions;

    /// <summary>
    /// The same descent over <see cref="BooleanResultBase.Underlying" /> rather than
    /// <see cref="BooleanResultBase.Causes" />, which is what separates <c>AllRootAssertions</c> from
    /// <c>RootAssertions</c>: operands that evaluated without determining the outcome count too.
    /// </summary>
    private static IEnumerable<string> LeafAssertions(BooleanResultBase result) =>
        result.Underlying.Any()
            ? result.Underlying.SelectMany(LeafAssertions)
            : result.Assertions;
}
