using Motiv.Traversal;

namespace Motiv.Tests.Traversal;

/// <summary>
/// Cover for ticket #136 — the question Spec 3A deferred. The three source walks are one algorithm;
/// <see cref="BooleanResultBase{TMetadata}.UnderlyingMetadataSources" /> had drifted from its two
/// assertion-source siblings in two ways, both of which are covered here: it yielded the result
/// <i>itself</i> once per non-operation child rather than the child the walk stopped at, and it had
/// no fallback for a result that contributed nothing.
/// </summary>
/// <remarks>
/// The last test covers the consequence the ticket did not name. `MetadataNode.Resolve` is the walk's
/// only in-library consumer, so correcting it also corrects the metadata tier tree — and with it the
/// public <see cref="BooleanResultBase{TMetadata}.RootValues" />, which was silently dropping the
/// values of contributing operands. Nothing in the suite covered that before, which is how it shipped.
/// </remarks>
public class UnderlyingMetadataSourcesTests
{
    private static SpecBase<string, string> Leaf(string name, bool value) =>
        Spec.Build((string _) => value).WhenTrue($"{name}-true").WhenFalse($"{name}-false").Create();

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

        // Nodes with no causal values are excluded because the ElseIfEmpty fallback makes such a node
        // its own source — which would be an operation result if one ever had an empty causal set. No
        // node in the vocabulary does today (see #188), so the filter narrows the count, not the claim.
        foreach (var root in ResultTreeGenerator.Corpus(seed))
        foreach (var node in ResultTreeGenerator.Nodes(root).Where(node => node.CausesWithValues.Any()))
        {
            examined++;
            node.UnderlyingMetadataSources.ShouldNotContain(
                source => source is IBooleanOperationResult,
                $"the walk descends through operations and stops at the operands that produced the " +
                $"values, so an operation node is never itself a source (seed {seed})");
        }

        examined.ShouldBeGreaterThan(0, "the invariant must actually have been exercised");
    }

    [Fact]
    public void Should_yield_itself_when_nothing_contributed()
    {
        var result = Leaf("a", true).Evaluate("model");

        result.UnderlyingMetadataSources.ShouldHaveSingleItem().ShouldBeSameAs(result);
    }

    [Theory]
    [MemberData(nameof(ResultTreeGenerator.SeedData), MemberType = typeof(ResultTreeGenerator))]
    public void Should_agree_with_its_assertion_source_sibling_wherever_the_causal_sets_agree(int seed)
    {
        var comparer = ReferenceComparer<BooleanResultBase>.Instance;
        var compared = 0;

        foreach (var root in ResultTreeGenerator.Corpus(seed))
        foreach (var node in ResultTreeGenerator.Nodes(root))
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
        foreach (var root in ResultTreeGenerator.Corpus(seed))
        foreach (var node in ResultTreeGenerator.Nodes(root).Where(node => !ContainsHigherOrder(node)))
            node.RootValues.ShouldBe(
                DistinctInOrder(CausalLeafValues(node)),
                $"RootValues is the metadata of every result that evaluated, so it must reach the " +
                $"causal leaves — descending CausesWithValues independently reaches the same set " +
                $"(seed {seed})");
    }

    /// <summary>
    /// Higher-order subtrees are excluded because <c>RootValues</c> still drops contributing operands
    /// there — issue #189, which #136's fix reduced but did not reach. The exclusion is exact rather
    /// than defensive: over the whole corpus this invariant holds at every one of the nodes without a
    /// higher-order result in the subtree, and fails only at those with one. Deleting this filter is
    /// the acceptance test for #189.
    /// </summary>
    private static bool ContainsHigherOrder(BooleanResultBase<string> result) =>
        result.GetType().Namespace?.StartsWith("Motiv.HigherOrderProposition", StringComparison.Ordinal) == true
        || result.UnderlyingWithValues.Any(ContainsHigherOrder);

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

    private static string[] DistinctInOrder(IEnumerable<string> values)
    {
        var seen = new HashSet<string>();
        var ordered = new List<string>();

        foreach (var value in values)
            if (seen.Add(value))
                ordered.Add(value);

        return ordered.ToArray();
    }
}
