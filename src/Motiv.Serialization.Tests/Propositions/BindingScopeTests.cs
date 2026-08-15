using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class BindingScopeTests
{
    private static SpecBase<int, string> AnySpec { get; } = Spec.Build((int n) => n > 0).Create("any");

    private static SpecRegistryEntry Entry(string name) =>
        new SpecRegistry().Register(name, AnySpec).Find(name)!;

    /// <summary>A participant that rebinds successfully, or fails on demand.</summary>
    private sealed class StubParticipant(NodeId node, bool succeeds) : IRebindable, IRebindCommit
    {
        public NodeId Node { get; } = node;
        public int PrepareCount { get; private set; }

        /// <summary>Set by the test to record the global commit order.</summary>
        public List<string>? OrderLog { get; init; }

        /// <summary>A name this participant resolves from the prospective source when preparing.</summary>
        public string? Resolves { get; init; }

        /// <summary>What <see cref="Resolves"/> resolved to on the last prepare, if anything.</summary>
        public SpecRegistryEntry? Resolved { get; private set; }

        public IRebindCommit? PrepareRebind(ISpecSource prospective, ScopeGeneration world, List<RuleError> errors)
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

        public void ApplyTo(ScopeGenerationBuilder builder)
        {
            // Only a proposition is referenceable, so only a proposition contributes an entry.
            if (Node.Kind == NodeKind.Proposition)
                builder.SetOverlayEntry(Entry(Node.Name));
        }
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

        scope.Mutate(builder => builder.SetOverlayEntry(Entry("authored")));
        scope.Source.Find("authored").ShouldNotBeNull();
    }

    /// <summary>
    /// Writing is live; reading is pinned — the split is drawn by what a caller does with the world,
    /// not by who calls it (see <c>BindingScope.Active</c>). <c>Source</c> is what documents *bind*
    /// against, and binding is on the writing side: a governed publish arriving on a pinned request
    /// must prepare against the world it will commit into, not the older one the request was pinned
    /// to.
    /// </summary>
    [Fact]
    public void Should_bind_through_the_live_world_even_while_a_pin_is_open()
    {
        // Arrange
        var scope = new BindingScope(new SpecRegistry());
        using var pin = scope.Pin();

        // Act — a publish lands while this flow is pinned
        scope.Mutate(builder => builder.SetOverlayEntry(Entry("authored")));

        // Assert — the pinned world is frozen, but the binding source has moved on
        scope.Active.Source.Find("authored").ShouldBeNull();
        scope.Source.Find("authored").ShouldNotBeNull();
    }

    [Fact]
    public void Should_prepare_nothing_when_the_closure_is_empty()
    {
        // Arrange
        var scope = new BindingScope(new SpecRegistry());
        var commits = new List<IRebindCommit>();

        // Act
        var broken = scope.PrepareClosure("a", new ScopeGenerationBuilder(scope.Registry, scope.Current), commits, []);

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
        scope.Mutate(builder => builder.Graph.Set(NodeId.Proposition("b"), ["a"]));
        scope.Mutate(builder => builder.Graph.Set(NodeId.Proposition("c"), ["b"]));
        var commits = new List<IRebindCommit>();

        // Act
        var broken = scope.PrepareClosure("a", new ScopeGenerationBuilder(scope.Registry, scope.Current), commits, []);

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
        var prospective = new ScopeGenerationBuilder(scope.Registry, scope.Current);
        scope.Enrol(new StubParticipant(NodeId.Proposition("b"), succeeds: true));
        scope.Mutate(builder => builder.Graph.Set(NodeId.Proposition("b"), ["a"]));

        // Act
        scope.PrepareClosure("a", prospective, [], []);

        // Assert
        prospective.Source.Find("b").ShouldNotBeNull();
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
        scope.Mutate(builder => builder.Graph.Set(NodeId.Proposition("b"), ["a"]));
        scope.Mutate(builder => builder.Graph.Set(NodeId.Proposition("c"), ["b"]));

        // Act
        scope.PrepareClosure("a", new ScopeGenerationBuilder(scope.Registry, scope.Current), [], []);

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
        scope.Mutate(builder => builder.Graph.Set(NodeId.Proposition("b"), ["a"]));

        // Act
        scope.PrepareClosure("a", new ScopeGenerationBuilder(scope.Registry, scope.Current), [], []);

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
        scope.Mutate(builder => builder.Graph.Set(NodeId.Proposition("b"), ["a"]));
        scope.Mutate(builder => builder.Graph.Set(NodeId.Rule("can-checkout"), ["a"]));
        var commits = new List<IRebindCommit>();

        // Act
        var broken = scope.PrepareClosure("a", new ScopeGenerationBuilder(scope.Registry, scope.Current), commits, []);

        // Assert
        broken.Count.ShouldBe(1);
        broken[0].Name.ShouldBe("can-checkout");
        broken[0].Kind.ShouldBe("rule");
        broken[0].Errors.ShouldNotBeEmpty();

        // Nothing went live. The one participant that did rebind wrote its entry into the prospective
        // builder, which this test never builds — that write is the whole of a commit now, so a
        // discarded builder is a discarded publish.
        good.PrepareCount.ShouldBe(1);
        scope.Source.Find("b").ShouldBeNull();
    }

    [Fact]
    public void Should_label_a_broken_proposition_dependent_as_a_proposition()
    {
        // Arrange
        var scope = new BindingScope(new SpecRegistry());
        scope.Enrol(new StubParticipant(NodeId.Proposition("b"), succeeds: false));
        scope.Mutate(builder => builder.Graph.Set(NodeId.Proposition("b"), ["a"]));

        // Act
        var broken = scope.PrepareClosure("a", new ScopeGenerationBuilder(scope.Registry, scope.Current), [], []);

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
        scope.Mutate(builder => builder.Graph.Set(NodeId.Proposition("b"), ["a"]));
        scope.Mutate(builder => builder.Graph.Set(NodeId.Rule("r"), ["a"]));

        // Act
        var broken = scope.PrepareClosure("a", new ScopeGenerationBuilder(scope.Registry, scope.Current), [], []);

        // Assert
        broken.Count.ShouldBe(2);
    }

    [Fact]
    public void Should_skip_closure_members_with_no_enrolled_participant()
    {
        // Arrange — a graph edge can outlive its participant during teardown; that must not throw
        var scope = new BindingScope(new SpecRegistry());
        scope.Mutate(builder => builder.Graph.Set(NodeId.Proposition("b"), ["a"]));

        // Act
        var broken = scope.PrepareClosure("a", new ScopeGenerationBuilder(scope.Registry, scope.Current), [], []);

        // Assert
        broken.ShouldBeEmpty();
    }

    /// <summary>
    /// Pins the exclusion set's three properties directly, at the level they actually live, rather
    /// than only through the governed-envelope tests in <c>GovernedPublishOrderingTests</c>: an
    /// excluded node is skipped by the walk (its own prepared change, elsewhere, is authoritative),
    /// its own dependents are still discovered and prepared regardless, and its entry in
    /// <paramref name="prospective"/> (a parameter of <see cref="BindingScope.PrepareClosure"/>) is
    /// left exactly as the caller set it — neither cleared nor overwritten.
    /// </summary>
    [Fact]
    public void Should_skip_an_excluded_node_but_still_prepare_its_own_dependents()
    {
        // Arrange — a <- b <- c, with b excluded. b's own prepare (elsewhere, not modelled here) is
        // what put its entry in the prospective overlay; that entry must survive this walk untouched.
        var scope = new BindingScope(new SpecRegistry());
        var prospective = new ScopeGenerationBuilder(scope.Registry, scope.Current);
        var bsOwnEntry = Entry("b");
        prospective.SetOverlayEntry(bsOwnEntry);

        var b = new StubParticipant(NodeId.Proposition("b"), succeeds: true);
        var c = new StubParticipant(NodeId.Proposition("c"), succeeds: true) { Resolves = "b" };
        scope.Enrol(b);
        scope.Enrol(c);
        scope.Mutate(builder => builder.Graph.Set(NodeId.Proposition("b"), ["a"]));
        scope.Mutate(builder => builder.Graph.Set(NodeId.Proposition("c"), ["b"]));
        var commits = new List<IRebindCommit>();

        // Act
        var broken = scope.PrepareClosure("a", prospective, commits, [NodeId.Proposition("b")]);

        // Assert
        broken.ShouldBeEmpty();

        // b is excluded: never rebound, and its own already-set entry survives — PrepareClosure must
        // not have called prospective.SetOverlayEntry for it at all.
        b.PrepareCount.ShouldBe(0);
        prospective.Source.Find("b").ShouldBeSameAs(bsOwnEntry);

        // c is not excluded — b's exclusion from being rebound does not exclude c, which references
        // b, from being discovered and prepared in its own right.
        c.PrepareCount.ShouldBe(1);
        commits.ShouldBe([c]);

        // c resolves "b" through the untouched entry b's own prepare set, not a rebind of it.
        c.Resolved.ShouldBeSameAs(bsOwnEntry);
    }

    [Fact]
    public void Should_stop_preparing_a_withdrawn_participant()
    {
        // Arrange
        var scope = new BindingScope(new SpecRegistry());
        var b = new StubParticipant(NodeId.Proposition("b"), succeeds: true);
        scope.Enrol(b);
        scope.Mutate(builder => builder.Graph.Set(NodeId.Proposition("b"), ["a"]));

        // Act
        scope.Withdraw(NodeId.Proposition("b"));
        scope.PrepareClosure("a", new ScopeGenerationBuilder(scope.Registry, scope.Current), [], []);

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
