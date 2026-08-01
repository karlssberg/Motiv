using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class DependencyGraphTests
{
    [Fact]
    public void Should_report_direct_referrers()
    {
        // Arrange
        var graph = new DependencyGraph();
        graph.Set(NodeId.Proposition("b"), ["a"]);
        graph.Set(NodeId.Proposition("c"), ["a"]);
        graph.Set(NodeId.Proposition("d"), ["b"]);

        // Act
        var referrers = graph.Referrers("a");

        // Assert
        referrers.ShouldBe([NodeId.Proposition("b"), NodeId.Proposition("c")], ignoreOrder: true);
    }

    [Fact]
    public void Should_report_rules_as_referrers_alongside_propositions()
    {
        // Arrange
        var graph = new DependencyGraph();
        graph.Set(NodeId.Proposition("b"), ["a"]);
        graph.Set(NodeId.Rule("can-checkout"), ["a"]);

        // Act
        var referrers = graph.Referrers("a");

        // Assert
        referrers.ShouldBe([NodeId.Proposition("b"), NodeId.Rule("can-checkout")], ignoreOrder: true);
    }

    [Fact]
    public void Should_keep_a_rule_and_a_proposition_of_the_same_name_distinct()
    {
        // Arrange — nothing stops a host naming a rule after a proposition; the graph must not merge them
        var graph = new DependencyGraph();
        graph.Set(NodeId.Proposition("shared"), ["a"]);
        graph.Set(NodeId.Rule("shared"), ["b"]);

        // Act & Assert
        graph.Referrers("a").ShouldBe([NodeId.Proposition("shared")]);
        graph.Referrers("b").ShouldBe([NodeId.Rule("shared")]);
    }

    [Fact]
    public void Should_report_the_transitive_closure_excluding_the_edited_node()
    {
        // Arrange — a <- b <- c, and a <- d
        var graph = new DependencyGraph();
        graph.Set(NodeId.Proposition("b"), ["a"]);
        graph.Set(NodeId.Proposition("c"), ["b"]);
        graph.Set(NodeId.Proposition("d"), ["a"]);

        // Act
        var closure = graph.DependentClosure("a");

        // Assert
        closure.ShouldBe(
            [NodeId.Proposition("b"), NodeId.Proposition("d"), NodeId.Proposition("c")],
            ignoreOrder: true);
        closure.ShouldNotContain(NodeId.Proposition("a"));
    }

    [Fact]
    public void Should_order_the_closure_dependencies_before_dependents()
    {
        // Arrange — a <- b <- c, and a shortcut d <- [a, c], so a naive breadth-first order (which
        // would discover d alongside b, at depth 1) is no longer accidentally correct
        var graph = new DependencyGraph();
        graph.Set(NodeId.Proposition("b"), ["a"]);
        graph.Set(NodeId.Proposition("c"), ["b"]);
        graph.Set(NodeId.Proposition("d"), ["a", "c"]);

        // Act
        var closure = graph.DependentClosure("a");

        // Assert
        closure.Count.ShouldBe(3);
        ShouldBeOrderedByDependency(closure, ("b", "c"), ("c", "d"));
    }

    [Fact]
    public void Should_order_a_diamond_so_both_sides_precede_the_join()
    {
        // Arrange — a <- b, a <- c, and d references both b and c
        var graph = new DependencyGraph();
        graph.Set(NodeId.Proposition("b"), ["a"]);
        graph.Set(NodeId.Proposition("c"), ["a"]);
        graph.Set(NodeId.Proposition("d"), ["b", "c"]);

        // Act
        var closure = graph.DependentClosure("a");

        // Assert — the join must come last; the order of b and c relative to each other is free
        closure.Count.ShouldBe(3);
        ShouldBeOrderedByDependency(closure, ("b", "d"), ("c", "d"));
    }

    /// <summary>
    /// The negative test that pins the ordering. If the topological sort is ever "simplified" to a
    /// plain reverse-BFS, every other cascade test still passes — wrong-order rebinding reports
    /// *fewer* errors, not different ones — so this is the only guard against silent
    /// under-reporting.
    /// </summary>
    [Fact]
    public void Should_never_place_a_dependent_before_something_it_depends_on()
    {
        // Arrange — a chain plus a shortcut edge, so a naive breadth-first order puts 'd' at depth 1
        var graph = new DependencyGraph();
        graph.Set(NodeId.Proposition("b"), ["a"]);
        graph.Set(NodeId.Proposition("c"), ["b"]);
        graph.Set(NodeId.Proposition("d"), ["a", "c"]);

        // Act
        var closure = graph.DependentClosure("a");

        // Assert — 'd' depends on 'c', so it must follow it despite also referencing 'a' directly
        ShouldBeOrderedByDependency(closure, ("c", "d"), ("b", "c"));
    }

    /// <summary>
    /// Asserts the ordering guarantee itself: every node appears after every node it depends on that
    /// is also in the closure. Order-agnostic, so it holds for any valid topological order rather than
    /// pinning one arbitrary sequence — which is what let a plain reachability walk pass before.
    /// </summary>
    private static void ShouldBeOrderedByDependency(
        IReadOnlyList<NodeId> closure, params (string Dependency, string Dependent)[] edges)
    {
        var positions = closure
            .Select((node, index) => (node, index))
            .ToDictionary(entry => entry.node, entry => entry.index);

        foreach (var (dependency, dependent) in edges)
        {
            var dependencyNode = NodeId.Proposition(dependency);
            var dependentNode = NodeId.Proposition(dependent);
            positions.ShouldContainKey(dependencyNode);
            positions.ShouldContainKey(dependentNode);
            positions[dependencyNode].ShouldBeLessThan(positions[dependentNode]);
        }
    }

    [Fact]
    public void Should_not_walk_past_a_rule_when_collecting_the_closure()
    {
        // Arrange — a <- rule 'mid' <- proposition 'q'. Documents reference specs, never rules, so 'q'
        // cannot really depend on a rule; the guard is what stops the walk treating it as if it could.
        var graph = new DependencyGraph();
        graph.Set(NodeId.Rule("mid"), ["a"]);
        graph.Set(NodeId.Proposition("q"), ["mid"]);

        // Act
        var closure = graph.DependentClosure("a");

        // Assert — the rule is rebound; 'q' is not dragged in behind it
        closure.ShouldBe([NodeId.Rule("mid")]);
    }

    [Fact]
    public void Should_detect_a_direct_self_reference()
    {
        // Arrange
        var graph = new DependencyGraph();

        // Act
        var cycle = graph.FindCycle("a", ["a"]);

        // Assert
        cycle.ShouldNotBeNull();
        cycle.ShouldBe(["a", "a"]);
    }

    [Fact]
    public void Should_detect_a_transitive_cycle()
    {
        // Arrange — b references a; giving a a reference to b closes the loop
        var graph = new DependencyGraph();
        graph.Set(NodeId.Proposition("b"), ["a"]);

        // Act
        var cycle = graph.FindCycle("a", ["b"]);

        // Assert
        cycle.ShouldNotBeNull();
        cycle.ShouldBe(["a", "b", "a"]);
    }

    [Fact]
    public void Should_allow_a_diamond_which_is_not_a_cycle()
    {
        // Arrange — b and c both reference a; d referencing both is a diamond, perfectly legal
        var graph = new DependencyGraph();
        graph.Set(NodeId.Proposition("b"), ["a"]);
        graph.Set(NodeId.Proposition("c"), ["a"]);

        // Act
        var cycle = graph.FindCycle("d", ["b", "c"]);

        // Assert
        cycle.ShouldBeNull();
    }

    [Fact]
    public void Should_ignore_references_to_names_with_no_edges_of_their_own()
    {
        // Arrange — a compiled spec has no outgoing edges and can never close a cycle
        var graph = new DependencyGraph();

        // Act
        var cycle = graph.FindCycle("a", ["compiled-spec"]);

        // Assert
        cycle.ShouldBeNull();
    }

    [Fact]
    public void Should_forget_a_removed_node()
    {
        // Arrange
        var graph = new DependencyGraph();
        graph.Set(NodeId.Proposition("b"), ["a"]);

        // Act
        graph.Remove(NodeId.Proposition("b"));

        // Assert
        graph.Referrers("a").ShouldBeEmpty();
        graph.DependentClosure("a").ShouldBeEmpty();
    }

    [Fact]
    public void Should_forget_a_removed_nodes_outgoing_edges_too()
    {
        // Arrange — the reverse index alone is not enough: FindCycle walks forward, over the
        // outgoing edges, so a removed node left with its own edges still closes phantom cycles
        var graph = new DependencyGraph();
        graph.Set(NodeId.Proposition("b"), ["a"]);

        // Act
        graph.Remove(NodeId.Proposition("b"));

        // Assert — b is gone, so pointing a at b cannot close a loop back to a
        graph.FindCycle("a", ["b"]).ShouldBeNull();
    }

    [Fact]
    public void Should_not_track_later_mutations_of_the_reference_list_it_was_given()
    {
        // Arrange — the graph must own its edges. Storing the caller's list by reference leaves the
        // forward walk reading edges that were never recorded: the reverse index would not know
        // about them, but FindCycle would follow them and report a cycle that does not exist.
        var graph = new DependencyGraph();
        var references = new List<string> { "x" };
        graph.Set(NodeId.Proposition("b"), references);

        // Act
        references.Add("a");

        // Assert — b's recorded edges are still just ["x"], so pointing a at b closes nothing
        graph.FindCycle("a", ["b"]).ShouldBeNull();
    }

    [Fact]
    public void Should_replace_a_nodes_edges_when_set_again()
    {
        // Arrange
        var graph = new DependencyGraph();
        graph.Set(NodeId.Proposition("b"), ["a"]);

        // Act — b is edited to reference c instead of a
        graph.Set(NodeId.Proposition("b"), ["c"]);

        // Assert
        graph.Referrers("a").ShouldBeEmpty();
        graph.Referrers("c").ShouldBe([NodeId.Proposition("b")]);
    }
}
