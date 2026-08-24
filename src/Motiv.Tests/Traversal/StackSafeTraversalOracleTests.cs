using Motiv.Shared;

namespace Motiv.Tests.Traversal;

/// <summary>
/// The acceptance gate for Spec 3A. Every result-tree walk is compared, at every node of every
/// generated tree, against <see cref="RecursiveTraversalOracle" /> — the recursion it replaced.
/// </summary>
public class StackSafeTraversalOracleTests
{
    private const int SeedCount = 150;

    public static TheoryData<int> Seeds
    {
        get
        {
            var data = new TheoryData<int>();
            for (var seed = 1; seed <= SeedCount; seed++)
                data.Add(seed);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Should_agree_with_the_recursive_oracle_at_every_node(int seed)
    {
        foreach (var root in ResultTreeGenerator.Corpus(seed))
        foreach (var node in ResultTreeGenerator.Nodes(root))
        {
            ShouldBeSameResults(
                node.UnderlyingAssertionSources,
                RecursiveTraversalOracle.UnderlyingAssertionSources(node),
                nameof(node.UnderlyingAssertionSources), seed);

            ShouldBeSameResults(
                node.UnderlyingAllAssertionSources,
                RecursiveTraversalOracle.UnderlyingAllAssertionSources(node),
                nameof(node.UnderlyingAllAssertionSources), seed);

            ShouldBeSameResults(
                node.UnderlyingMetadataSources,
                RecursiveTraversalOracle.UnderlyingMetadataSources(node),
                nameof(node.UnderlyingMetadataSources), seed);

            ShouldBeSameResults(
                node.UnderlyingExpressionResults,
                RecursiveTraversalOracle.UnderlyingExpressionResults(node),
                nameof(node.UnderlyingExpressionResults), seed);

            ShouldBeSameStrings(
                node.UnderlyingReasons,
                RecursiveTraversalOracle.UnderlyingReasons(node),
                nameof(node.UnderlyingReasons), seed);

            ShouldBeSameStrings(
                node.AllAssertions,
                RecursiveTraversalOracle.AllAssertions(node),
                nameof(node.AllAssertions), seed);

            ShouldBeSameStrings(
                node.Causes.GetAssertions(),
                RecursiveTraversalOracle.GetAssertions(node.Causes),
                "GetAssertions", seed);

            ShouldBeSameStrings(
                node.Underlying.GetAllAssertions(),
                RecursiveTraversalOracle.GetAllAssertions(node.Underlying),
                "GetAllAssertions", seed);

            ShouldBeSameStrings(
                node.RootAssertions,
                RecursiveTraversalOracle.GetRootAssertions(node),
                nameof(node.RootAssertions), seed);

            ShouldBeSameStrings(
                node.AllRootAssertions,
                RecursiveTraversalOracle.GetAllRootAssertions(node),
                nameof(node.AllRootAssertions), seed);

            ShouldBeSameExplanations(
                node.Explanation.Underlying,
                RecursiveTraversalOracle.ExplanationUnderlying(node.Explanation),
                "Explanation.Underlying", seed);

            ShouldBeSameExplanations(
                node.Explanation.AllUnderlying,
                RecursiveTraversalOracle.ExplanationAllUnderlying(node.Explanation),
                "Explanation.AllUnderlying", seed);
        }
    }

    [Fact]
    public void Should_generate_a_corpus_containing_the_short_circuited_shape()
    {
        var shortCircuited = AllNodes()
            .OfType<Motiv.Traversal.IBinaryBooleanOperationResult>()
            .Any(binary => binary.Right is null);

        shortCircuited.ShouldBeTrue(
            "the corpus must contain a short-circuited operand, which is the shape the fold is most " +
            "likely to get wrong");
    }

    [Fact]
    public void Should_generate_a_corpus_containing_every_operation()
    {
        var operations = AllNodes()
            .OfType<Motiv.Traversal.IBinaryBooleanOperationResult>()
            .Select(binary => binary.Operation)
            .Distinct()
            .ToArray();

        operations.ShouldBe(
            [Operator.And, Operator.Or, Operator.XOr, Operator.AndAlso, Operator.OrElse],
            ignoreOrder: true);
    }

    private static IEnumerable<BooleanResultBase<string>> AllNodes() =>
        Enumerable.Range(1, SeedCount)
            .SelectMany(ResultTreeGenerator.Corpus)
            .SelectMany(ResultTreeGenerator.Nodes);

    private static void ShouldBeSameResults<T>(
        IEnumerable<T> actual,
        IEnumerable<T> oracle,
        string member,
        int seed)
        where T : BooleanResultBase =>
        actual.SequenceEqual(oracle, ReferenceComparer<T>.Instance)
            .ShouldBeTrue($"{member} diverged from the recursive oracle (seed {seed})");

    private static void ShouldBeSameStrings(
        IEnumerable<string> actual,
        IEnumerable<string> oracle,
        string member,
        int seed) =>
        actual.ToArray().ShouldBe(oracle.ToArray(), $"{member} diverged from the recursive oracle (seed {seed})");

    private static void ShouldBeSameExplanations(
        IEnumerable<Explanation> actual,
        IEnumerable<Explanation> oracle,
        string member,
        int seed) =>
        actual.SequenceEqual(oracle, ReferenceComparer<Explanation>.Instance)
            .ShouldBeTrue($"{member} diverged from the recursive oracle (seed {seed})");
}
