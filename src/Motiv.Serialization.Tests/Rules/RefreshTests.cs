using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Rules;

public class RefreshTests
{
    private static SpecBase<int, string> Positive { get; } =
        Spec.Build((int n) => n > 0).Create("positive");

    private sealed class NumberRule() : Rule<int, string>("number", Positive);

    /// <summary>
    /// The async twin of <see cref="Positive"/> and <see cref="NumberRule"/>, registered under its
    /// own name so a document can address either flavour without the two colliding. Exists so this
    /// file's refresh scenarios can be replayed against <see cref="AsyncRule{TModel,TMetadata}"/> —
    /// otherwise every rule this file ever refreshes is synchronous, and a refresh calls
    /// <c>AsyncRule.BindStoredState</c>, <c>WithVersion</c> and <c>DocumentJsonIn</c> on nothing.
    /// </summary>
    private static AsyncSpecBase<int, string> PositiveAsync { get; } =
        Spec.BuildAsync(async (int n) => { await Task.Yield(); return n > 0; }).Create("positive-async");

    private sealed class NumberAsyncRule() : AsyncRule<int, string>("number-async", PositiveAsync);

    private const string NotPositive = """{"rule":{"not":{"spec":"positive"}}}""";
    private const string IsPositive = """{"rule":{"spec":"positive"}}""";
    private const string OnlyInTheNewBuild = """{"rule":{"spec":"only-in-the-new-build"}}""";

    private const string NotPositiveAsync = """{"rule":{"not":{"spec":"positive-async"}}}""";
    private const string IsPositiveAsync = """{"rule":{"spec":"positive-async"}}""";

    private static RuleChangeProvenance By(string author) => new(author);

    /// <summary>One build's compiled catalog.</summary>
    /// <param name="extraSpecs">
    /// Names this build knows and another's need not. A replica is a *build* as much as a process,
    /// and the interesting refresh failures are the ones where the two builds differ.
    /// </param>
    private static SpecRegistry RegistryWith(params string[] extraSpecs)
    {
        var registry = new SpecRegistry()
            .Register("positive", Positive)
            .Register("positive-async", PositiveAsync);
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

    /// <summary>The async twin of <see cref="Replica"/>, for scenarios that need an async rule on the wire.</summary>
    private static (RuleSet Rules, NumberAsyncRule Rule) AsyncReplica(IRuleStore store, params string[] extraSpecs)
    {
        var rules = new RuleSet(RegistryWith(extraSpecs), store);
        var rule = new NumberAsyncRule();
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

    /// <summary>
    /// The async counterpart of <see cref="Should_converge_a_second_replica_on_the_first_replicas_publish"/>.
    /// Every rule this file otherwise refreshes is a synchronous <c>Rule&lt;int,string&gt;</c>, so a
    /// rebuild's calls into <c>AsyncRule.BindStoredState</c> and <c>DocumentJsonIn</c> — the members it
    /// exercises on every registered rule, sync or async — were reached by nothing. This is a real gap:
    /// the sample host's <c>FraudScreeningRule</c> is declared as an <c>AsyncRule</c>, so any replica
    /// running it takes exactly this path on every refresh.
    /// </summary>
    [Fact]
    public async Task Should_converge_a_second_replica_on_the_first_replicas_publish_of_an_async_rule()
    {
        // Arrange — one store, two replicas, one of them running an async rule
        var store = new InMemoryRuleStore();
        var (a, _) = AsyncReplica(store);
        var (b, ruleB) = AsyncReplica(store);

        // Act
        await a.UpdateAsync("number-async", NotPositiveAsync, 1, By("alice"));
        var report = await b.RefreshAsync(default);

        // Assert — B was serving yesterday's policy and now is not
        report.Outcome.ShouldBe(RefreshOutcome.Applied);
        b.FindEntry("number-async")!.Version.ShouldBe(2);
        (await ruleB.EvaluateAsync(1)).Satisfied.ShouldBeFalse();

        // And the replica now knows where it stands, so the next tick is the cheap path rather than
        // a rebuild of a world it already has
        (await b.RefreshAsync(default)).Outcome.ShouldBe(RefreshOutcome.Unchanged);
    }

    /// <summary>
    /// <c>AsyncRule.WithVersion</c> is not reached by a refresh at all — <c>RebuildHeadsInto</c> binds
    /// a rebuild's state straight from the store's own version. It is reached by <see cref="RuleSet.Load"/>,
    /// which renumbers a freshly-bound document to the version the store actually holds rather than the
    /// version <see cref="RuleSet.Add"/> guessed. A replica that loads after two publishes have already
    /// happened is the case that tells the two apart: had <c>WithVersion</c> not run (or renumbered to
    /// the wrong thing), this replica would report version 2 — the number its own bind produced — and
    /// every future publish against it would spuriously version-conflict with the store's real head.
    /// </summary>
    [Fact]
    public async Task Should_restore_the_stores_version_for_an_async_rule_loaded_after_two_publishes()
    {
        // Arrange — A publishes twice before B is ever created, so B's Load — not a refresh — is what
        // binds the stored head
        var store = new InMemoryRuleStore();
        var (a, _) = AsyncReplica(store);
        await a.UpdateAsync("number-async", NotPositiveAsync, 1, By("alice"));
        await a.UpdateAsync("number-async", IsPositiveAsync, 2, By("alice"));

        // Act
        var (b, ruleB) = AsyncReplica(store);

        // Assert — the store's version (3), not the version a from-scratch bind would have minted (2)
        b.FindEntry("number-async")!.Version.ShouldBe(3);
        (await ruleB.EvaluateAsync(1)).Satisfied.ShouldBeTrue();
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
        // Arrange — the proposition has to exist before either replica's rule default can bind
        var propositionStore = new InMemoryPropositionStore();
        var ruleStore = new InMemoryRuleStore();

        var propsA = new PropositionSet(RegistryWith(), propositionStore).AddModel<int>("number");
        propsA.Load();
        await propsA.CreateAsync("gate", "number", IsPositive, null);
        var rulesA = new RuleSet(propsA, ruleStore);
        rulesA.Add(new GatedRule());
        rulesA.Load();

        var propsB = new PropositionSet(RegistryWith(), propositionStore).AddModel<int>("number");
        propsB.Load();
        var rulesB = new RuleSet(propsB, ruleStore);
        var ruleB = new GatedRule();
        rulesB.Add(ruleB);
        rulesB.Load();
        ruleB.Evaluate(1).Satisfied.ShouldBeTrue();

        // Act — A moves the store, so B genuinely rebuilds; only then does the proposition beneath
        // B's own rule change, locally
        await propsA.CreateAsync("spare", "number", IsPositive, null);
        (await rulesB.RefreshAsync(default)).Outcome.ShouldBe(RefreshOutcome.Applied);
        await propsB.UpdateAsync("gate", NotPositive, 1);

        // Assert — the cascade still reaches a rule the refresh re-bound
        ruleB.Evaluate(1).Satisfied.ShouldBeFalse();
    }

    /// <summary>
    /// The one state in which a rule ends the rebuild with <em>no binding at all</em>: its default is
    /// a document, and the proposition that document references was quarantined by this same refresh.
    /// Both halves of the handling are exercised here, and neither is reachable any other way.
    /// </summary>
    /// <remarks>
    /// The rule is reported as a regression rather than carried, because a slot with no state has
    /// nothing for a quarantine to hang off and nothing to evaluate — a rule must always be able to
    /// evaluate. And the head loop has to skip it: reaching <c>SetRuleQuarantine</c> on an empty slot
    /// throws, which would turn a refusal to converge into a crashing poller.
    /// </remarks>
    [Fact]
    public async Task Should_refuse_when_a_rules_document_default_loses_the_proposition_it_needs()
    {
        // Arrange — A's build knows a spec B's does not, and publishes through it on both sides
        var propositionStore = new InMemoryPropositionStore();
        var ruleStore = new InMemoryRuleStore();

        var propsA = new PropositionSet(RegistryWith("only-in-the-new-build"), propositionStore)
            .AddModel<int>("number");
        propsA.Load();
        await propsA.CreateAsync("gate", "number", IsPositive, null);

        var rulesA = new RuleSet(propsA, ruleStore);
        rulesA.Add(new GatedRule());
        rulesA.Load();
        await rulesA.UpdateAsync("gated", OnlyInTheNewBuild, 1, By("alice"));

        // B binds the proposition, then quarantines the rule head it cannot resolve — so it is
        // running its document default, over a proposition it still resolves
        var propsB = new PropositionSet(RegistryWith(), propositionStore).AddModel<int>("number");
        propsB.Load();
        var rulesB = new RuleSet(propsB, ruleStore);
        var ruleB = new GatedRule();
        rulesB.Add(ruleB);
        rulesB.Load();
        rulesB.FindEntry("gated")!.Quarantine.ShouldNotBeEmpty();

        // Act — the proposition the default leans on becomes unbindable for B too
        await propsA.UpdateAsync("gate", OnlyInTheNewBuild, 1);
        var report = await rulesB.RefreshAsync(default);

        // Assert — both halves refused, and B is untouched
        report.Outcome.ShouldBe(RefreshOutcome.Aborted);
        report.Regressions.Count.ShouldBe(2);
        report.Regressions.ShouldContain(failure => failure.Name == "gate" && failure.Kind == "proposition");
        report.Regressions.ShouldContain(failure => failure.Name == "gated" && failure.Kind == "rule");
        ruleB.Evaluate(1).Satisfied.ShouldBeTrue();
        propsB.Find("gate")!.Quarantine.ShouldBeEmpty();
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

    /// <summary>
    /// The refresh's own compare-and-set, driven end to end rather than by calling
    /// <c>TrySwap</c> directly. A publish that lands <em>while a rebuild is in flight</em> must
    /// survive it: the rebuild read its heads before that publish existed, so swapping its world in
    /// would silently revert a durable write — the lost update the write stamp exists to refuse.
    /// </summary>
    /// <remarks>
    /// The publish is injected from inside <see cref="IRuleStore.LoadAsync"/>, after the heads have
    /// been handed back. By then the refresh has taken its snapshot and read its rows, so "mid-flight"
    /// is true by construction — no threads, no latch, no timing, and no seam in the production code.
    /// </remarks>
    [Fact]
    public async Task Should_refuse_a_swap_for_a_publish_that_landed_while_it_was_rebuilding()
    {
        // Arrange
        var inner = new InMemoryRuleStore();
        var store = new PublishingRuleStore(inner);
        var (a, ruleA) = Replica(store);

        // Another replica's row, so this one has something to refresh towards without its own world
        // having moved
        (await inner.AppendAsync([Ghost(1)], default)).IsConflict.ShouldBeFalse();

        // Act — one local publish, landing after the rebuild has read the store
        store.OnLoaded = () => a.UpdateAsync("number", NotPositive, 1, By("alice"));
        var report = await a.RefreshAsync(default);

        // Assert — the publish is intact. A rebuild that swapped in would have put the rule back on
        // its compiled default at version 1, because the heads it built from predate the publish.
        a.FindEntry("number")!.Version.ShouldBe(2);
        ruleA.Evaluate(1).Satisfied.ShouldBeFalse();

        // And the retry converges rather than declaring itself done. The publish carried this replica
        // past its own row and no further: the foreign row it has never read is still outstanding, and
        // the position the publish read back from the store is not one this world may claim — see
        // ScopeGenerationBuilder.AdvanceRuleSequence.
        report.Outcome.ShouldBe(RefreshOutcome.Applied);
    }

    /// <summary>
    /// Losing the swap three times running is reported rather than retried forever. A refresh that
    /// keeps being overtaken is being overtaken by publishes, and each one is a world at least as new
    /// as the one it was building — so the honest answer is to say so and let the next tick try.
    /// </summary>
    [Fact]
    public async Task Should_report_contention_when_a_publish_beats_every_attempt()
    {
        // Arrange
        var inner = new InMemoryRuleStore();
        var store = new PublishingRuleStore(inner);
        var (a, _) = Replica(store);
        (await inner.AppendAsync([Ghost(1)], default)).IsConflict.ShouldBeFalse();

        // Act — every attempt is overtaken. The local publish moves this replica's write stamp and so
        // refuses the swap in flight; the foreign row after it keeps the store ahead of what that
        // publish recorded, so the next attempt still has something to converge towards.
        var ghost = 1;
        store.OnLoaded = async () =>
        {
            await a.UpdateAsync("number", NotPositive, a.FindEntry("number")!.Version, By("alice"));
            (await inner.AppendAsync([Ghost(++ghost)], default)).IsConflict.ShouldBeFalse();
        };

        var report = await a.RefreshAsync(default);

        // Assert
        report.Outcome.ShouldBe(RefreshOutcome.Contended);
        report.IsConverged.ShouldBeFalse();
        store.Loads.ShouldBe(3);

        // All three publishes survived; not one was overwritten by the rebuild racing it
        a.FindEntry("number")!.Version.ShouldBe(4);
    }

    private sealed class OtherRule() : Rule<int, string>("other", Positive);

    /// <summary>
    /// An abort reports both halves. The pass that found the blocker also found everything already
    /// broken, and an operator diagnosing a replica that has stopped converging needs both — one of
    /// the carried rows may well be *why* the other regressed. Discarding the carried list because
    /// "nothing was applied" throws away evidence gathered at no extra cost.
    /// </summary>
    [Fact]
    public async Task Should_report_what_was_already_broken_even_when_it_aborts()
    {
        // Arrange — two rules over one store, and two builds that disagree about one spec
        var store = new InMemoryRuleStore();

        var rulesA = new RuleSet(RegistryWith("only-in-the-new-build"), store);
        rulesA.Add(new NumberRule());
        rulesA.Add(new OtherRule());
        rulesA.Load();

        // 'other' is unbindable for B from the moment B first sees it; 'number' is not
        await rulesA.UpdateAsync("other", OnlyInTheNewBuild, 1, By("alice"));
        await rulesA.UpdateAsync("number", NotPositive, 1, By("alice"));

        var rulesB = new RuleSet(RegistryWith(), store);
        rulesB.Add(new NumberRule());
        rulesB.Add(new OtherRule());
        rulesB.Load();
        rulesB.FindEntry("other")!.Quarantine.ShouldNotBeEmpty();
        rulesB.FindEntry("number")!.Quarantine.ShouldBeEmpty();

        // Act — 'number' now regresses too, while 'other' stays exactly as broken as it was
        await rulesA.UpdateAsync("number", OnlyInTheNewBuild, 2, By("alice"));
        await rulesA.UpdateAsync("other", OnlyInTheNewBuild, 2, By("alice"));
        var report = await rulesB.RefreshAsync(default);

        // Assert — the blocker and the pre-existing damage, side by side
        report.Outcome.ShouldBe(RefreshOutcome.Aborted);
        report.Regressions.ShouldHaveSingleItem();
        report.Regressions[0].Name.ShouldBe("number");
        report.Quarantined.ShouldHaveSingleItem();
        report.Quarantined[0].Name.ShouldBe("other");
    }

    /// <summary>
    /// The fencing token has to be right on a replica that has only ever loaded. It is stamped on
    /// every response so a client can tell it was routed to a replica serving an older world, and a
    /// replica reporting <see cref="StoreGeneration.Zero"/> forever does not merely fail to help — two
    /// replicas holding different worlds would stamp the same token, which is worse than none, because
    /// it looks authoritative.
    /// </summary>
    [Fact]
    public async Task Should_stamp_where_the_stores_stood_at_load()
    {
        // Arrange — a store that already holds someone else's writes, as a restarting pod's does
        var propositionStore = new InMemoryPropositionStore();
        var ruleStore = new InMemoryRuleStore();
        var (propsA, rulesA, _) = PairedReplica(propositionStore, ruleStore);
        await propsA.CreateAsync("negative", "number", NotPositive, null);
        await rulesA.UpdateAsync("number", NotPositive, 1, By("alice"));

        // Act — a fresh replica that loads and never refreshes
        var (_, rulesB, _) = PairedReplica(propositionStore, ruleStore);

        // Assert — both halves recorded, and agreeing with the replica that wrote them
        using var snapshot = rulesB.PinSnapshot();
        snapshot.Generation.ShouldNotBe(StoreGeneration.Zero);
        snapshot.Generation.ShouldBe(rulesA.Scope.Current.Sequence);
    }

    /// <summary>
    /// And on a replica that publishes. Between a local write and the next poll tick the token would
    /// otherwise describe the world this replica served a moment ago — stale in the one direction that
    /// matters, since a client reading its own write back would be told it was behind.
    /// </summary>
    [Fact]
    public async Task Should_move_the_stamp_on_a_local_publish_without_a_refresh()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        var (a, _) = Replica(store);
        var before = a.Scope.Current.Sequence;

        // Act — no refresh anywhere in this test
        await a.UpdateAsync("number", NotPositive, 1, By("alice"));

        // Assert — read back from the store, not counted locally: a replica that incremented its own
        // counter would agree with one that had published something else entirely
        var after = a.Scope.Current.Sequence;
        after.ShouldNotBe(before);
        after.Rules.ShouldBe(await store.GetGenerationAsync(default));
    }

    /// <summary>
    /// The regression check reads one world, never two. <c>rule.DocumentJson</c> reads whatever is
    /// live at the instant it is called, and a rebuild has held no lock since it snapshotted — so
    /// pairing the snapshot's slot with the live document manufactures a regression out of a state no
    /// world ever held. That is not merely a bad report: an abort returns <em>before</em>
    /// <c>TrySwap</c>, so a mismatch invented here escapes the stamp check that exists to adjudicate
    /// exactly this race, and the replica refuses to converge on the strength of it.
    /// </summary>
    /// <remarks>
    /// The load count is what separates the invented refusal from the honest one, because the two end
    /// in the same outcome. An abort returns immediately, so a first attempt that invented a
    /// regression would have read the store exactly once. Reading it twice means the first attempt
    /// carried the unbindable head — the correct judgement against the world it snapshotted, in which
    /// this rule had no document — lost the swap to the publish, and only <em>then</em> refused, on
    /// the strength of the approved document that publish had by that point genuinely committed.
    /// </remarks>
    [Fact]
    public async Task Should_judge_a_regression_against_one_world_rather_than_two()
    {
        // Arrange — this replica is on its compiled default: bound, unquarantined, no document
        var inner = new InMemoryRuleStore();
        var store = new PublishingRuleStore(inner);
        var (a, ruleA) = Replica(store);
        a.FindEntry("number")!.DocumentJson.ShouldBeNull();

        // A newer build's row, which this one cannot bind
        (await inner.AppendAsync([Row("number", 3, OnlyInTheNewBuild)], default)).IsConflict.ShouldBeFalse();

        // Act — a local publish lands mid-rebuild and gives the rule a document, so the live world and
        // the snapshotted one now disagree about precisely the fact the check reads
        store.OnLoaded = () => a.UpdateAsync("number", NotPositive, 1, By("alice"));
        var report = await a.RefreshAsync(default);

        // Assert — the first attempt judged against its own snapshot and carried on; the refusal that
        // did arrive belongs to the second, and to a document the publish had really committed
        store.Loads.ShouldBe(2);
        report.Outcome.ShouldBe(RefreshOutcome.Aborted);
        report.Regressions.ShouldHaveSingleItem();
        report.Regressions[0].Name.ShouldBe("number");

        // And the publish stands either way
        a.FindEntry("number")!.Version.ShouldBe(2);
        ruleA.Evaluate(1).Satisfied.ShouldBeFalse();
    }

    /// <summary>Another replica's row under a name this build has no rule for: it moves the store's generation and nothing else.</summary>
    private static StoredRuleVersion Ghost(int version) => Row("ghost", version, null);

    /// <summary>A row as another replica, or a hand-edited store, would have left it.</summary>
    private static StoredRuleVersion Row(string name, int version, string? documentJson) =>
        new(name, version, documentJson, "another-replica", DateTimeOffset.UtcNow, null, null, "test");

    /// <summary>
    /// A store that lets a test publish from inside the refresh's own read. The callback runs
    /// <em>after</em> the heads have been handed back, which is what makes the rebuild's world stale
    /// by construction rather than by luck.
    /// </summary>
    private sealed class PublishingRuleStore(IRuleStore inner) : IRuleStore
    {
        public Func<Task>? OnLoaded { get; set; }

        public int Loads { get; private set; }

        public IReadOnlyList<StoredRule> Load() => inner.Load();

        public async Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken ct)
        {
            Loads++;
            var heads = await inner.LoadAsync(ct);

            if (OnLoaded is { } publish)
                await publish();

            return heads;
        }

        public Task<long> GetGenerationAsync(CancellationToken ct) => inner.GetGenerationAsync(ct);

        public Task<RuleAppendResult> AppendAsync(IReadOnlyList<StoredRuleVersion> versions, CancellationToken ct) =>
            inner.AppendAsync(versions, ct);

        public Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(string name, CancellationToken ct) =>
            inner.HistoryAsync(name, ct);
    }

    private const string ViaA = """{"rule":{"spec":"a"}}""";
    private const string NotViaA = """{"rule":{"not":{"spec":"a"}}}""";

    /// <summary>A replica whose authored propositions are all it has — the simplest shape a scope has.</summary>
    private static PropositionSet PropositionReplica(IPropositionStore store)
    {
        var propositions = new PropositionSet(RegistryWith(), store).AddModel<int>("number");
        propositions.Load();
        return propositions;
    }

    /// <summary>
    /// A publish reads where the store stands <em>after</em> its own write, and that number speaks for
    /// every row in the store rather than only the one it wrote. Another replica's write landing
    /// between this replica's last poll and this publish is therefore swallowed: the world does not
    /// hold that row, but the sequence claims the position that includes it, so <c>MovedFrom</c> is
    /// false on every subsequent tick and the poller never rebuilds. The replica serves the row it
    /// last read until some unrelated write happens to move the store again — silent, unbounded
    /// divergence on a replica whose health check reads green throughout.
    /// </summary>
    [Fact]
    public async Task Should_not_claim_a_proposition_store_position_whose_rows_it_has_not_read()
    {
        // Arrange — two replicas over one store, both holding a and b at version 1
        var store = new InMemoryPropositionStore();
        var a = PropositionReplica(store);
        await a.CreateAsync("a", "number", IsPositive, null);
        await a.CreateAsync("b", "number", ViaA, null);

        var b = PropositionReplica(store);
        b.Find("b")!.Version.ShouldBe(1);

        // Act — A publishes while B is between polls, then B publishes something of its own
        await a.UpdateAsync("b", NotViaA, 1);
        (await b.UpdateAsync("a", NotPositive, 1)).Outcome.ShouldBe(PropositionUpdateOutcome.Updated);

        // Assert — B's own publish must not have claimed A's, which B has never read
        (await b.RefreshAsync()).Outcome.ShouldBe(RefreshOutcome.Applied);
        b.Find("b")!.Version.ShouldBe(2);
        b.DocumentJsonOf("b")!.ShouldBe(NotViaA);
    }

    /// <summary>The rule half of <see cref="Should_not_claim_a_proposition_store_position_whose_rows_it_has_not_read"/>.</summary>
    [Fact]
    public async Task Should_not_claim_a_rule_store_position_whose_rows_it_has_not_read()
    {
        // Arrange — two replicas over one store, each carrying both rules
        var store = new InMemoryRuleStore();
        var a = TwoRuleReplica(store);
        var b = TwoRuleReplica(store);

        // Act — A publishes a rule B is not publishing, then B publishes one of its own
        await a.UpdateAsync("other", NotPositive, 1, By("alice"));
        (await b.UpdateAsync("number", NotPositive, 1, By("bob"))).Outcome
            .ShouldBe(RuleUpdateOutcome.Updated);

        // Assert — B's own publish must not have claimed A's, which B has never read
        (await b.RefreshAsync(default)).Outcome.ShouldBe(RefreshOutcome.Applied);
        b.FindEntry("other")!.Version.ShouldBe(2);
    }

    private static RuleSet TwoRuleReplica(IRuleStore store)
    {
        var rules = new RuleSet(RegistryWith(), store);
        rules.Add(new NumberRule());
        rules.Add(new OtherRule());
        rules.Load();
        return rules;
    }

    /// <summary>
    /// The other side of <see cref="Should_refuse_a_swap_for_a_publish_that_landed_while_it_was_rebuilding"/>:
    /// a refresh landing inside a publish's store round trip. The publish holds the outer gate but no
    /// monitor while it awaits the store, and it captured nothing that would tell it the world had
    /// moved — so it resumes and forks the <em>refreshed</em> world, writing its prepared bindings and
    /// its cascaded rebinds over it. Those rebinds carry the pre-refresh document and version of every
    /// dependent, because a rebind deliberately carries the version across unchanged. The result is a
    /// world no store state ever held: a dependent reverted, an unrelated row left refreshed, and a
    /// sequence claiming both.
    /// </summary>
    /// <remarks>
    /// The refresh is injected from inside <see cref="IPropositionStore.WriteAsync"/>, so "mid-flight"
    /// is true by construction — the publish has prepared and released the monitor, and has not yet
    /// read back the generation it will stamp. No threads, no latch, no timing.
    /// </remarks>
    [Fact]
    public async Task Should_refuse_a_refresh_that_a_publish_would_commit_over()
    {
        // Arrange — b references a and would be rebound by a's publish; c is unrelated to all of it
        var inner = new InMemoryPropositionStore();
        var remote = PropositionReplica(inner);
        await remote.CreateAsync("a", "number", IsPositive, null);
        await remote.CreateAsync("b", "number", ViaA, null);
        await remote.CreateAsync("c", "number", IsPositive, null);

        var store = new RefreshingPropositionStore(inner);
        var local = PropositionReplica(store);

        // The other replica moves both rows while this one sits between polls
        await remote.UpdateAsync("b", NotViaA, 1);
        await remote.UpdateAsync("c", NotPositive, 1);

        // Act — one refresh, landing after this publish's row is durable and before it commits
        RefreshReport? midFlight = null;
        store.OnWritten = async () => midFlight = await local.RefreshAsync();
        (await local.UpdateAsync("a", NotPositive, 1)).Outcome.ShouldBe(PropositionUpdateOutcome.Updated);

        // Assert — the refresh lost rather than being half-committed over. The world this replica now
        // serves is exactly the one it was serving plus its own publish: b and c both untouched.
        midFlight!.Outcome.ShouldBe(RefreshOutcome.Contended);
        local.Find("a")!.Version.ShouldBe(2);
        local.Find("b")!.Version.ShouldBe(1);
        local.Find("c")!.Version.ShouldBe(1);

        // And the replica still converges: losing a swap is a retry, not a wedge
        (await local.RefreshAsync()).Outcome.ShouldBe(RefreshOutcome.Applied);
        local.Find("a")!.Version.ShouldBe(2);
        local.Find("b")!.Version.ShouldBe(2);
        local.Find("c")!.Version.ShouldBe(2);
        local.DocumentJsonOf("b")!.ShouldBe(NotViaA);
    }

    /// <summary>
    /// A publish that throws must not leave itself registered as in flight. The failure mode is
    /// asymmetric and worth a test of its own: a leaked registration is never cleared by anything, so
    /// one store outage would stop this replica converging for the life of the process — a strictly
    /// worse outcome than the interleave the registration exists to refuse, and one no health check
    /// would attribute to the write that caused it.
    /// </summary>
    [Fact]
    public async Task Should_not_leave_a_failed_publish_registered_as_in_flight()
    {
        // Arrange
        var inner = new InMemoryPropositionStore();
        var remote = PropositionReplica(inner);
        await remote.CreateAsync("a", "number", IsPositive, null);

        var store = new RefreshingPropositionStore(inner);
        var local = PropositionReplica(store);
        await remote.UpdateAsync("a", NotPositive, 1);

        // Act — the store throws from inside the publish, after the registration was taken
        store.OnWritten = () => throw new InvalidOperationException("the store is unreachable");
        await Should.ThrowAsync<InvalidOperationException>(
            local.CreateAsync("b", "number", IsPositive, null));

        // Assert — the replica still converges
        (await local.RefreshAsync()).Outcome.ShouldBe(RefreshOutcome.Applied);
        local.DocumentJsonOf("a")!.ShouldBe(NotPositive);
    }

    /// <summary>
    /// A store that lets a test refresh from inside a publish's own write. The callback runs
    /// <em>after</em> the row is durable, which is exactly where the publish sits when it holds the
    /// outer gate and no monitor. Fires once: a refresh reads this same store, and re-entering would
    /// prove nothing the first pass has not.
    /// </summary>
    private sealed class RefreshingPropositionStore(IPropositionStore inner) : IPropositionStore
    {
        public Func<Task>? OnWritten { get; set; }

        public IReadOnlyList<StoredProposition> Load() => inner.Load();

        public Task<IReadOnlyList<StoredProposition>> LoadAsync(CancellationToken ct) => inner.LoadAsync(ct);

        public Task<long> GetGenerationAsync(CancellationToken ct) => inner.GetGenerationAsync(ct);

        public async Task WriteAsync(PropositionBatch batch, CancellationToken ct)
        {
            await inner.WriteAsync(batch, ct);

            if (OnWritten is not { } refresh)
                return;

            OnWritten = null;
            await refresh();
        }
    }

    [Fact]
    public async Task Should_read_only_the_generation_when_nothing_has_moved()
    {
        // Arrange
        var store = new CountingRuleStore(new InMemoryRuleStore());
        var (a, _) = Replica(store);

        // Load reads both — the rows, and the generation it stamps on the world it builds. Zeroed so
        // what follows counts only the refresh's own reads.
        store.Loads = 0;
        store.GenerationReads = 0;

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
        public int GenerationReads { get; set; }

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
