using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class BindingScopeTests
{
    private static SpecBase<int, string> AnySpec { get; } = Spec.Build((int n) => n > 0).Create("any");

    private static SpecRegistryEntry Entry(string name) =>
        new SpecRegistry().Register(name, AnySpec).Find(name)!;

    /// <summary>A participant that rebinds successfully, or fails on demand, and records that it committed.</summary>
    private sealed class StubParticipant(NodeId node, bool succeeds) : IRebindable, IRebindCommit
    {
        public NodeId Node { get; } = node;
        public bool Committed { get; private set; }
        public int PrepareCount { get; private set; }

        /// <summary>Set by the test to record the global commit order.</summary>
        public List<string>? OrderLog { get; init; }

        /// <summary>A name this participant resolves from the prospective source when preparing.</summary>
        public string? Resolves { get; init; }

        /// <summary>What <see cref="Resolves"/> resolved to on the last prepare, if anything.</summary>
        public SpecRegistryEntry? Resolved { get; private set; }

        public SpecRegistryEntry? OverlayEntry =>
            Node.Kind == NodeKind.Proposition ? Entry(Node.Name) : null;

        public IRebindCommit? PrepareRebind(ISpecSource prospective, List<RuleError> errors)
        {
            PrepareCount++;
            OrderLog?.Add(Node.Name);
            if (Resolves is { } name)
                Resolved = prospective.Find(name);
            if (succeeds)
                return this;
            errors.Add(new RuleError("$", RuleErrorCode.UnknownSpec, $"{Node.Name} cannot bind"));
            return null;
        }

        public void Commit() => Committed = true;
    }

    [Fact]
    public void Should_expose_a_layered_source_over_the_registry()
    {
        // Arrange
        var registry = new SpecRegistry().Register("compiled", AnySpec);
        var scope = new BindingScope(registry);

        // Act & Assert
        scope.Source.Find("compiled").ShouldNotBeNull();
        scope.Source.Find("authored").ShouldBeNull();

        scope.Overlay.Set(Entry("authored"));
        scope.Source.Find("authored").ShouldNotBeNull();
    }

    [Fact]
    public void Should_prepare_nothing_when_the_closure_is_empty()
    {
        // Arrange
        var scope = new BindingScope(new SpecRegistry());
        var commits = new List<IRebindCommit>();

        // Act
        var broken = scope.PrepareClosure("a", new PropositionOverlay(), commits);

        // Assert
        broken.ShouldBeEmpty();
        commits.ShouldBeEmpty();
    }

    [Fact]
    public void Should_prepare_every_dependent_in_dependency_order()
    {
        // Arrange — a <- b <- c
        var scope = new BindingScope(new SpecRegistry());
        var order = new List<string>();
        var b = new StubParticipant(NodeId.Proposition("b"), succeeds: true) { OrderLog = order };
        var c = new StubParticipant(NodeId.Proposition("c"), succeeds: true) { OrderLog = order };
        scope.Enrol(b);
        scope.Enrol(c);
        scope.Graph.Set(NodeId.Proposition("b"), ["a"]);
        scope.Graph.Set(NodeId.Proposition("c"), ["b"]);
        var commits = new List<IRebindCommit>();

        // Act
        var broken = scope.PrepareClosure("a", new PropositionOverlay(), commits);

        // Assert
        broken.ShouldBeEmpty();
        order.ShouldBe(["b", "c"]);
        commits.Count.ShouldBe(2);
    }

    [Fact]
    public void Should_fold_each_prepared_entry_into_the_prospective_overlay()
    {
        // Arrange — 'c' must be able to see the freshly bound 'b' while preparing
        var scope = new BindingScope(new SpecRegistry());
        var prospective = new PropositionOverlay();
        scope.Enrol(new StubParticipant(NodeId.Proposition("b"), succeeds: true));
        scope.Graph.Set(NodeId.Proposition("b"), ["a"]);

        // Act
        scope.PrepareClosure("a", prospective, []);

        // Assert
        prospective.Find("b").ShouldNotBeNull();
    }

    [Fact]
    public void Should_let_a_later_closure_member_resolve_an_earlier_members_fresh_binding()
    {
        // Arrange — a <- b <- c. 'c' prepares after 'b', so it must see the entry 'b' just contributed,
        // not whatever the live overlay holds. This is the whole reason the closure is ordered.
        var scope = new BindingScope(new SpecRegistry());
        var c = new StubParticipant(NodeId.Proposition("c"), succeeds: true) { Resolves = "b" };
        scope.Enrol(new StubParticipant(NodeId.Proposition("b"), succeeds: true));
        scope.Enrol(c);
        scope.Graph.Set(NodeId.Proposition("b"), ["a"]);
        scope.Graph.Set(NodeId.Proposition("c"), ["b"]);

        // Act
        scope.PrepareClosure("a", new PropositionOverlay(), []);

        // Assert
        c.Resolved.ShouldNotBeNull();
    }

    [Fact]
    public void Should_only_prepare_nodes_in_the_dependent_closure()
    {
        // Arrange — 'x' is enrolled but not reachable from 'a'; the walk must be driven by the graph
        // closure, not by iterating every enrolled participant.
        var scope = new BindingScope(new SpecRegistry());
        var b = new StubParticipant(NodeId.Proposition("b"), succeeds: true);
        var x = new StubParticipant(NodeId.Proposition("x"), succeeds: true);
        scope.Enrol(b);
        scope.Enrol(x);
        scope.Graph.Set(NodeId.Proposition("b"), ["a"]);

        // Act
        scope.PrepareClosure("a", new PropositionOverlay(), []);

        // Assert
        b.PrepareCount.ShouldBe(1);
        x.PrepareCount.ShouldBe(0);
    }

    [Fact]
    public void Should_report_a_broken_dependent_without_committing_anything()
    {
        // Arrange
        var scope = new BindingScope(new SpecRegistry());
        var good = new StubParticipant(NodeId.Proposition("b"), succeeds: true);
        var bad = new StubParticipant(NodeId.Rule("can-checkout"), succeeds: false);
        scope.Enrol(good);
        scope.Enrol(bad);
        scope.Graph.Set(NodeId.Proposition("b"), ["a"]);
        scope.Graph.Set(NodeId.Rule("can-checkout"), ["a"]);
        var commits = new List<IRebindCommit>();

        // Act
        var broken = scope.PrepareClosure("a", new PropositionOverlay(), commits);

        // Assert
        broken.Count.ShouldBe(1);
        broken[0].Name.ShouldBe("can-checkout");
        broken[0].Kind.ShouldBe("rule");
        broken[0].Errors.ShouldNotBeEmpty();
        good.Committed.ShouldBeFalse();
        bad.Committed.ShouldBeFalse();
    }

    [Fact]
    public void Should_label_a_broken_proposition_dependent_as_a_proposition()
    {
        // Arrange
        var scope = new BindingScope(new SpecRegistry());
        scope.Enrol(new StubParticipant(NodeId.Proposition("b"), succeeds: false));
        scope.Graph.Set(NodeId.Proposition("b"), ["a"]);

        // Act
        var broken = scope.PrepareClosure("a", new PropositionOverlay(), []);

        // Assert
        broken.Count.ShouldBe(1);
        broken[0].Kind.ShouldBe("proposition");
    }

    [Fact]
    public void Should_collect_every_broken_dependent_rather_than_stopping_at_the_first()
    {
        // Arrange — reporting only the first would make a wide break take many round trips to diagnose
        var scope = new BindingScope(new SpecRegistry());
        scope.Enrol(new StubParticipant(NodeId.Proposition("b"), succeeds: false));
        scope.Enrol(new StubParticipant(NodeId.Rule("r"), succeeds: false));
        scope.Graph.Set(NodeId.Proposition("b"), ["a"]);
        scope.Graph.Set(NodeId.Rule("r"), ["a"]);

        // Act
        var broken = scope.PrepareClosure("a", new PropositionOverlay(), []);

        // Assert
        broken.Count.ShouldBe(2);
    }

    [Fact]
    public void Should_skip_closure_members_with_no_enrolled_participant()
    {
        // Arrange — a graph edge can outlive its participant during teardown; that must not throw
        var scope = new BindingScope(new SpecRegistry());
        scope.Graph.Set(NodeId.Proposition("b"), ["a"]);

        // Act
        var broken = scope.PrepareClosure("a", new PropositionOverlay(), []);

        // Assert
        broken.ShouldBeEmpty();
    }

    [Fact]
    public void Should_stop_preparing_a_withdrawn_participant()
    {
        // Arrange
        var scope = new BindingScope(new SpecRegistry());
        var b = new StubParticipant(NodeId.Proposition("b"), succeeds: true);
        scope.Enrol(b);
        scope.Graph.Set(NodeId.Proposition("b"), ["a"]);

        // Act
        scope.Withdraw(NodeId.Proposition("b"));
        scope.PrepareClosure("a", new PropositionOverlay(), []);

        // Assert
        b.PrepareCount.ShouldBe(0);
    }

    [Fact]
    public void Should_run_the_supplied_action_under_the_lock_and_return_its_value()
    {
        // Arrange
        var scope = new BindingScope(new SpecRegistry());

        // Act
        var result = scope.Locked(() => 42);

        // Assert
        result.ShouldBe(42);
    }

    [Fact]
    public void Should_serialize_concurrent_locked_sections()
    {
        // Arrange — a data race here would surface as a count below the expected total
        var scope = new BindingScope(new SpecRegistry());
        var counter = 0;

        // Act
        Parallel.For(0, 200, _ => scope.Locked(() =>
        {
            var seen = counter;
            counter = seen + 1;
            return 0;
        }));

        // Assert
        counter.ShouldBe(200);
    }
}
