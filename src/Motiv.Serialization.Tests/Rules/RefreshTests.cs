using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Rules;

public class RefreshTests
{
    private static SpecBase<int, string> Positive { get; } =
        Spec.Build((int n) => n > 0).Create("positive");

    private sealed class NumberRule() : Rule<int, string>("number", Positive);

    private const string NotPositive = """{"rule":{"not":{"spec":"positive"}}}""";
    private const string IsPositive = """{"rule":{"spec":"positive"}}""";
    private const string OnlyInTheNewBuild = """{"rule":{"spec":"only-in-the-new-build"}}""";

    private static RuleChangeProvenance By(string author) => new(author);

    /// <summary>One build's compiled catalog.</summary>
    /// <param name="extraSpecs">
    /// Names this build knows and another's need not. A replica is a *build* as much as a process,
    /// and the interesting refresh failures are the ones where the two builds differ.
    /// </param>
    private static SpecRegistry RegistryWith(params string[] extraSpecs)
    {
        var registry = new SpecRegistry().Register("positive", Positive);
        foreach (var name in extraSpecs)
            registry.Register(name, Positive);

        return registry;
    }

    /// <summary>Two independent replicas over one store — the shape a second pod has.</summary>
    /// <param name="store">The store both replicas share, as two pods share one database.</param>
    /// <param name="extraSpecs">See <see cref="RegistryWith"/>.</param>
    private static (RuleSet Rules, NumberRule Rule) Replica(IRuleStore store, params string[] extraSpecs)
    {
        var rules = new RuleSet(RegistryWith(extraSpecs), store);
        var rule = new NumberRule();
        rules.Add(rule);
        rules.Load();
        return (rules, rule);
    }

    [Fact]
    public async Task Should_converge_a_second_replica_on_the_first_replicas_publish()
    {
        // Arrange — one store, two replicas, as two pods behind a load balancer
        var store = new InMemoryRuleStore();
        var (a, _) = Replica(store);
        var (b, ruleB) = Replica(store);

        // Act
        await a.UpdateAsync("number", NotPositive, 1, By("alice"));
        var report = await b.RefreshAsync(default);

        // Assert — B was serving yesterday's policy and now is not
        report.Outcome.ShouldBe(RefreshOutcome.Applied);
        b.FindEntry("number")!.Version.ShouldBe(2);
        ruleB.Evaluate(1).Satisfied.ShouldBeFalse();

        // And the replica now knows where it stands, so the next tick is the cheap path rather than
        // a rebuild of a world it already has
        (await b.RefreshAsync(default)).Outcome.ShouldBe(RefreshOutcome.Unchanged);
    }

    /// <summary>
    /// The second refresh, which is the one that matters: a replica that has converged once must be
    /// able to converge again. A rebuild that carried the version check of an ordinary publish would
    /// pass here only while the replica was still on version 1, and abort permanently thereafter —
    /// green on a single-refresh suite, and a replica frozen on yesterday's world in production.
    /// </summary>
    [Fact]
    public async Task Should_converge_again_on_a_second_publish()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        var (a, _) = Replica(store);
        var (b, ruleB) = Replica(store);

        await a.UpdateAsync("number", NotPositive, 1, By("alice"));
        (await b.RefreshAsync(default)).Outcome.ShouldBe(RefreshOutcome.Applied);

        // Act — a second publish, and a second refresh of a replica no longer on version 1
        await a.UpdateAsync("number", IsPositive, 2, By("alice"));
        var report = await b.RefreshAsync(default);

        // Assert
        report.Outcome.ShouldBe(RefreshOutcome.Applied);
        b.FindEntry("number")!.Version.ShouldBe(3);
        ruleB.Evaluate(1).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_do_nothing_when_the_store_has_not_moved()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        var (a, _) = Replica(store);
        var stamp = a.Scope.WriteStamp;

        // Act
        var report = await a.RefreshAsync(default);

        // Assert — the cheap path: no rebuild, no swap, no allocation of a world
        report.Outcome.ShouldBe(RefreshOutcome.Unchanged);
        a.Scope.WriteStamp.ShouldBe(stamp);
    }

    [Fact]
    public async Task Should_keep_serving_when_a_stored_document_would_regress_a_live_rule()
    {
        // Arrange — two builds over one store. A's build knows a spec B's does not, which is what a
        // rolling deployment looks like from the old pod's side.
        var store = new InMemoryRuleStore();
        var (a, _) = Replica(store, "only-in-the-new-build");

        // Something both builds can bind, so B has a live, approved, non-default binding to protect
        await a.UpdateAsync("number", NotPositive, 1, By("alice"));

        var (b, ruleB) = Replica(store);
        b.FindEntry("number")!.Version.ShouldBe(2);

        // A publishes something only its own build can resolve
        await a.UpdateAsync("number", OnlyInTheNewBuild, 2, By("alice"));

        // Act
        var report = await b.RefreshAsync(default);

        // Assert — B keeps the approved behaviour it was serving rather than dropping to the
        // compiled default, and says why
        report.Outcome.ShouldBe(RefreshOutcome.Aborted);
        report.IsConverged.ShouldBeFalse();
        report.Regressions.ShouldNotBeEmpty();
        report.Regressions[0].Name.ShouldBe("number");
        report.Regressions[0].Kind.ShouldBe("rule");
        b.FindEntry("number")!.Version.ShouldBe(2);
        ruleB.Evaluate(1).Satisfied.ShouldBeFalse();
    }

    /// <summary>
    /// The carried half of the split: a row that is already quarantined has no live binding to
    /// protect, so it must never block convergence. Read literally, "abort when anything fails" would
    /// stall this replica on the bad row forever and it would converge on nothing.
    /// </summary>
    [Fact]
    public async Task Should_carry_a_quarantine_forward_rather_than_let_it_block_convergence()
    {
        // Arrange — B has never bound the stored document, so it starts out quarantined
        var store = new InMemoryRuleStore();
        var (a, _) = Replica(store, "only-in-the-new-build");
        await a.UpdateAsync("number", OnlyInTheNewBuild, 1, By("alice"));

        var (b, ruleB) = Replica(store);
        b.FindEntry("number")!.Quarantine.ShouldNotBeEmpty();

        // Act — the store moves again, still with a document B cannot bind
        await a.UpdateAsync("number", OnlyInTheNewBuild, 2, By("alice"));
        var report = await b.RefreshAsync(default);

        // Assert — applied, with the quarantine reported rather than blocking
        report.Outcome.ShouldBe(RefreshOutcome.Applied);
        report.IsConverged.ShouldBeTrue();
        report.Regressions.ShouldBeEmpty();
        report.Quarantined.ShouldNotBeEmpty();
        report.Quarantined[0].Name.ShouldBe("number");
        b.FindEntry("number")!.Version.ShouldBe(3);
        b.FindEntry("number")!.Quarantine.ShouldNotBeEmpty();
        ruleB.Evaluate(1).Satisfied.ShouldBeTrue();
    }

    /// <summary>
    /// The two halves of a scope rebuild in one swap, and in dependency order. The rule head this
    /// replica is converging on references an authored proposition that exists only in the store, so
    /// rebuilding the rules first would leave that name unresolvable and quarantine a rule that is
    /// perfectly bindable.
    /// </summary>
    [Fact]
    public async Task Should_rebuild_the_authored_layer_before_the_rules_that_reference_it()
    {
        // Arrange
        var propositionStore = new InMemoryPropositionStore();
        var ruleStore = new InMemoryRuleStore();
        var (propsA, rulesA, _) = PairedReplica(propositionStore, ruleStore);
        var (_, rulesB, ruleB) = PairedReplica(propositionStore, ruleStore);

        // Act — a proposition and a rule that references it, both published on A only
        await propsA.CreateAsync("negative", "number", NotPositive, null);
        await rulesA.UpdateAsync("number", """{"rule":{"spec":"negative"}}""", 1, By("alice"));

        var report = await rulesB.RefreshAsync(default);

        // Assert — B resolved a name that reached it in the very same rebuild
        report.Outcome.ShouldBe(RefreshOutcome.Applied);
        report.Quarantined.ShouldBeEmpty();
        report.Regressions.ShouldBeEmpty();
        ruleB.Evaluate(1).Satisfied.ShouldBeFalse();
    }

    /// <summary>
    /// A quarantined authored proposition must survive the rebuild as a listed, repairable document.
    /// It has no binding, so a world that cannot resolve it looks like a world with no business
    /// carrying it — and dropping it would remove the operator's only view of the thing they have to
    /// fix, silently, with every other assertion still green. <c>LoadOne</c> carries the same warning.
    /// </summary>
    [Fact]
    public async Task Should_carry_a_proposition_it_cannot_bind_rather_than_drop_it()
    {
        // Arrange — two builds again, this time differing over a proposition's document
        var propositionStore = new InMemoryPropositionStore();
        var ruleStore = new InMemoryRuleStore();
        var (propsA, _, _) = PairedReplica(propositionStore, ruleStore, "only-in-the-new-build");
        var (propsB, rulesB, _) = PairedReplica(propositionStore, ruleStore);

        // Act
        await propsA.CreateAsync("negative", "number", OnlyInTheNewBuild, null);
        var report = await rulesB.RefreshAsync(default);

        // Assert — carried, not blocking, and still visible to every read an operator has
        report.Outcome.ShouldBe(RefreshOutcome.Applied);
        report.Regressions.ShouldBeEmpty();
        report.Quarantined.ShouldHaveSingleItem();
        report.Quarantined[0].Name.ShouldBe("negative");
        report.Quarantined[0].Kind.ShouldBe("proposition");
        propsB.Find("negative")!.Quarantine.ShouldNotBeEmpty();
        propsB.DocumentJsonOf("negative")!.ShouldBe(OnlyInTheNewBuild);
        propsB.Propositions.ShouldContain(entry => entry.Name == "negative");
    }

    /// <summary>
    /// The proposition half of the regression rule. A proposition is <em>referenceable</em>, so
    /// letting one stop binding takes every dependent down with it — which is exactly why a refresh
    /// refuses rather than applying and reporting.
    /// </summary>
    [Fact]
    public async Task Should_keep_serving_when_a_stored_proposition_would_regress_a_live_one()
    {
        // Arrange
        var propositionStore = new InMemoryPropositionStore();
        var ruleStore = new InMemoryRuleStore();
        var (propsA, _, _) = PairedReplica(propositionStore, ruleStore, "only-in-the-new-build");
        await propsA.CreateAsync("negative", "number", NotPositive, null);

        // B is built afterwards, so it loads and binds the proposition as it stands
        var (propsB, rulesB, _) = PairedReplica(propositionStore, ruleStore);
        propsB.Find("negative")!.Quarantine.ShouldBeEmpty();

        // Act — A replaces it with something only A's build can resolve
        await propsA.UpdateAsync("negative", OnlyInTheNewBuild, 1);
        var report = await rulesB.RefreshAsync(default);

        // Assert — B keeps the definition it was resolving
        report.Outcome.ShouldBe(RefreshOutcome.Aborted);
        report.Regressions.ShouldHaveSingleItem();
        report.Regressions[0].Name.ShouldBe("negative");
        report.Regressions[0].Kind.ShouldBe("proposition");
        propsB.Find("negative")!.Quarantine.ShouldBeEmpty();
        propsB.DocumentJsonOf("negative")!.ShouldBe(NotPositive);
    }

    private sealed class GatedRule()
        : Rule<int, string>("gated", RuleDocuments.FromJson("""{"rule":{"spec":"gate"}}"""));

    /// <summary>
    /// A rebuilt world has to carry the rebind graph, not merely the bindings. A rule declared with a
    /// document default references propositions from the moment it is added, so a refresh that
    /// re-bound it without re-recording its edges would leave it bound correctly and <em>silently
    /// unenrolled</em> — every later proposition publish would stop cascading to it, and the rule
    /// would go on resolving a definition nobody could see it was resolving.
    /// </summary>
    [Fact]
    public async Task Should_keep_a_document_defaults_edges_across_a_rebuild()
    {
        // Arrange — the proposition has to exist before the rule's default can bind against it
        var propositions = new PropositionSet(RegistryWith(), new InMemoryPropositionStore()).AddModel<int>("number");
        propositions.Load();
        await propositions.CreateAsync("gate", "number", IsPositive, null);

        var rules = new RuleSet(propositions, new InMemoryRuleStore());
        var rule = new GatedRule();
        rules.Add(rule);
        rules.Load();
        rule.Evaluate(1).Satisfied.ShouldBeTrue();

        // Act — a rebuild, and only then the proposition beneath the rule moves
        (await rules.RefreshAsync(default)).Outcome.ShouldBe(RefreshOutcome.Applied);
        await propositions.UpdateAsync("gate", NotPositive, 1);

        // Assert — the cascade still reaches a rule the refresh re-bound
        rule.Evaluate(1).Satisfied.ShouldBeFalse();
    }

    /// <summary>A replica whose rules and authored propositions share one scope, as a real host's do.</summary>
    private static (PropositionSet Propositions, RuleSet Rules, NumberRule Rule) PairedReplica(
        IPropositionStore propositionStore, IRuleStore ruleStore, params string[] extraSpecs)
    {
        var propositions = new PropositionSet(RegistryWith(extraSpecs), propositionStore).AddModel<int>("number");
        propositions.Load();

        var rules = new RuleSet(propositions, ruleStore);
        var rule = new NumberRule();
        rules.Add(rule);
        rules.Load();
        return (propositions, rules, rule);
    }

    [Fact]
    public async Task Should_discard_a_rebuild_that_a_publish_beat_to_the_swap()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        var (a, ruleA) = Replica(store);
        await a.UpdateAsync("number", NotPositive, 1, By("alice"));

        // Act — a local publish lands while a refresh is mid-flight, simulated by moving the world
        // between the stamp being taken and the swap being attempted
        var stamp = a.Scope.WriteStamp;
        var successor = a.Scope.Current;
        await a.UpdateAsync("number", IsPositive, 2, By("alice"));
        var swapped = a.Scope.Locked(() => a.Scope.TrySwap(successor, stamp));

        // Assert — the publish survives; the stale rebuild does not overwrite it
        swapped.ShouldBeFalse();
        ruleA.Evaluate(1).Satisfied.ShouldBeTrue();
        a.FindEntry("number")!.Version.ShouldBe(3);
    }

    [Fact]
    public async Task Should_read_only_the_generation_when_nothing_has_moved()
    {
        // Arrange
        var store = new CountingRuleStore(new InMemoryRuleStore());
        var (a, _) = Replica(store);
        store.Loads = 0;

        // Act
        await a.RefreshAsync(default);

        // Assert — a poll that loaded the store would defeat the entire point: every replica does
        // this on a timer
        store.Loads.ShouldBe(0);
        store.GenerationReads.ShouldBe(1);
    }

    private sealed class CountingRuleStore(IRuleStore inner) : IRuleStore
    {
        public int Loads { get; set; }
        public int GenerationReads { get; private set; }

        public IReadOnlyList<StoredRule> Load() => inner.Load();

        public Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken ct)
        {
            Loads++;
            return inner.LoadAsync(ct);
        }

        public Task<long> GetGenerationAsync(CancellationToken ct)
        {
            GenerationReads++;
            return inner.GetGenerationAsync(ct);
        }

        public Task<RuleAppendResult> AppendAsync(IReadOnlyList<StoredRuleVersion> versions, CancellationToken ct) =>
            inner.AppendAsync(versions, ct);

        public Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(string name, CancellationToken ct) =>
            inner.HistoryAsync(name, ct);
    }
}
