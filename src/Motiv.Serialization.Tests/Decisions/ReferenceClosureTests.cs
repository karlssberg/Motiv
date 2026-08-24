namespace Motiv.Serialization.Tests.Decisions;

/// <summary>
/// The forward walk of the dependency graph: everything a node's meaning depends on. The reverse of
/// <c>DependentClosure</c>, and what pins a decision record's third anchor.
/// </summary>
public class ReferenceClosureTests
{
    private static DependencyGraph AGraph(params (string Node, string[] References)[] edges)
    {
        var graph = new DependencyGraph();
        foreach (var (node, references) in edges)
            graph.Set(NodeId.Proposition(node), references);
        return graph;
    }

    [Fact]
    public void Should_return_nothing_for_a_node_with_no_references()
    {
        // Arrange
        var graph = AGraph(("a", []));

        // Act / Assert
        graph.ReferenceClosure(NodeId.Proposition("a")).ShouldBeEmpty();
    }

    [Fact]
    public void Should_return_nothing_for_an_unknown_node()
    {
        // Act / Assert — a rule on a compiled default has no edges, and that is an answer
        new DependencyGraph().ReferenceClosure(NodeId.Rule("unregistered")).ShouldBeEmpty();
    }

    [Fact]
    public void Should_return_direct_references()
    {
        // Arrange
        var graph = new DependencyGraph();
        graph.Set(NodeId.Rule("can-checkout"), ["customer.is-active", "customer.is-adult"]);

        // Act
        var closure = graph.ReferenceClosure(NodeId.Rule("can-checkout"));

        // Assert
        closure.ShouldBe(["customer.is-active", "customer.is-adult"], ignoreOrder: true);
    }

    [Fact]
    public void Should_follow_references_transitively()
    {
        // Arrange — a rule that reaches customer.is-active only through pricing.eligible still
        // changes behaviour when customer.is-active is republished
        var graph = AGraph(
            ("pricing.eligible", ["customer.is-active"]),
            ("customer.is-active", ["customer.exists"]));
        graph.Set(NodeId.Rule("can-checkout"), ["pricing.eligible"]);

        // Act
        var closure = graph.ReferenceClosure(NodeId.Rule("can-checkout"));

        // Assert
        closure.ShouldBe(["pricing.eligible", "customer.is-active", "customer.exists"], ignoreOrder: true);
    }

    [Fact]
    public void Should_report_a_diamond_once()
    {
        // Arrange
        var graph = AGraph(
            ("left", ["shared"]),
            ("right", ["shared"]),
            ("shared", []));
        graph.Set(NodeId.Rule("top"), ["left", "right"]);

        // Act
        var closure = graph.ReferenceClosure(NodeId.Rule("top"));

        // Assert
        closure.Count(name => name == "shared").ShouldBe(1);
    }

    [Fact]
    public void Should_exclude_the_node_itself()
    {
        // Arrange — the graph refuses cycles at publish time, but the walk must terminate on one
        // regardless rather than trusting a check it does not perform
        var graph = AGraph(("a", ["b"]), ("b", ["a"]));

        // Act
        var closure = graph.ReferenceClosure(NodeId.Proposition("a"));

        // Assert
        closure.ShouldBe(["b", "a"], ignoreOrder: true);
    }
}
