namespace Motiv.Serialization.Tests.Governance;

/// <summary>
/// Task 8: a governed publish persists the whole envelope — both the rule half and the proposition
/// half, each as one store batch — before applying any of it. See CLAUDE.md's "Post-Implementation
/// Code Review" and the task-8 plan for the shape: prepare all, persist all, apply all.
/// </summary>
public class GovernedPublishOrderingTests
{
    private sealed record Customer(bool IsActive, int Age);

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static PolicyBase<Customer, string> IsActivePolicy { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    /// <summary>Composition returns a Spec, not a Policy, so this is what a PolicyRule must refuse to rebind to.</summary>
    private static SpecBase<Customer, string> ComposedNonPolicy { get; } = IsActive & IsActive;

    private sealed class ARule() : Rule<Customer, string>("a", IsActive);

    private sealed class BRule() : Rule<Customer, string>("b", IsActive);

    private sealed class CanCheckoutPolicyRule() : PolicyRule<Customer, string>("can-checkout-policy", IsActivePolicy);

    /// <summary>
    /// Bound fresh, straight off <c>Scope.Source</c>, whenever a test needs to observe what
    /// "customer.p2" *actually* resolves to after a publish — not what <see cref="PropositionSet.DocumentJsonOf"/>
    /// or a reported version says, since a stale overlay entry in the published generation is
    /// invisible to both.
    /// </summary>
    private sealed class CheckRule()
        : Rule<Customer, string>("check", RuleDocuments.FromJson("""{ "rule": { "spec": "customer.p2" } }"""));

    private const string Document = """{ "rule": { "spec": "customer.is-active" } }""";

    /// <summary>Refuses appends after the Nth call, so the envelope's persist phase can fail.</summary>
    private sealed class RefusingRuleStore(int refuseAfter) : IRuleStore
    {
        private readonly InMemoryRuleStore _inner = new();
        private int _appends;

        public int Appends => _appends;

        public IReadOnlyList<StoredRule> Load() => _inner.Load();
        public Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken ct) => _inner.LoadAsync(ct);
        public Task<long> GetGenerationAsync(CancellationToken ct) => _inner.GetGenerationAsync(ct);
        public Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(string n, CancellationToken ct) =>
            _inner.HistoryAsync(n, ct);

        public Task<RuleAppendResult> AppendAsync(
            IReadOnlyList<StoredRuleVersion> versions, CancellationToken ct) =>
            ++_appends > refuseAfter
                ? Task.FromResult(RuleAppendResult.Conflict(versions[0].Name, 99))
                : _inner.AppendAsync(versions, ct);
    }

    /// <summary>Counts batches without refusing any of them, so a test can assert "one call" directly.</summary>
    private sealed class CountingPropositionStore : IPropositionStore
    {
        private readonly InMemoryPropositionStore _inner = new();
        public int Writes { get; private set; }

        public IReadOnlyList<StoredProposition> Load() => _inner.Load();
        public Task<IReadOnlyList<StoredProposition>> LoadAsync(CancellationToken ct) => _inner.LoadAsync(ct);
        public Task<long> GetGenerationAsync(CancellationToken ct) => _inner.GetGenerationAsync(ct);

        public Task WriteAsync(PropositionBatch batch, CancellationToken cancellationToken)
        {
            Writes++;
            return _inner.WriteAsync(batch, cancellationToken);
        }
    }

    /// <summary>
    /// Simulates the infrastructure fault <see cref="ChangeRequestOutcome.PersistenceDesynced"/>
    /// exists for: an exception, not a business refusal — <see cref="IPropositionStore"/> has no
    /// conflict result to return instead.
    /// </summary>
    private sealed class ThrowingPropositionStore : IPropositionStore
    {
        public IReadOnlyList<StoredProposition> Load() => [];
        public Task<IReadOnlyList<StoredProposition>> LoadAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<StoredProposition>>([]);
        public Task<long> GetGenerationAsync(CancellationToken ct) => Task.FromResult(0L);

        public Task WriteAsync(PropositionBatch batch, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("simulated proposition store outage");
    }

    private sealed record Host(ChangeRequestSet Governance, RuleSet Rules, PropositionSet Propositions);

    private static Host Harness(IRuleStore ruleStore, IPropositionStore? propositionStore = null)
    {
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var scope = new BindingScope(registry);
        var propositions = new PropositionSet(scope, propositionStore ?? new InMemoryPropositionStore())
            .AddModel<Customer>("customer");
        var rules = new RuleSet(scope, ruleStore).Add(new ARule()).Add(new BRule());
        var gate = new ApprovalGate();
        return new Host(new ChangeRequestSet(gate, rules, propositions), rules, propositions);
    }

    /// <summary>Creates a draft change request proposing a document update for each named rule.</summary>
    private static ChangeRequest Envelope(ChangeRequestSet governance, params (string Name, string Document)[] edits)
    {
        var changes = edits
            .Select(edit => new NewProposedChange(
                ChangeTargetKind.Rule, edit.Name, edit.Document, BaseVersion: 1, RollbackOfVersion: null))
            .ToList();

        var created = governance.Create("alice", "batch update", changes);
        created.Outcome.ShouldBe(ChangeRequestOutcome.Ok);
        return created.Change!;
    }

    [Fact]
    public async Task Should_write_one_batch_for_a_whole_envelope()
    {
        // Arrange — two rules in one change request must persist as one store call, not two
        var store = new RefusingRuleStore(refuseAfter: 1);
        var host = Harness(store);
        var envelope = Envelope(host.Governance, ("a", Document), ("b", Document));

        // Act
        var result = await host.Governance.PublishAsync(envelope.Id, breakGlassActive: false);

        // Assert — one batch means one append, so refuseAfter: 1 lets it through
        result.Outcome.ShouldBe(ChangeRequestOutcome.Ok);
        store.Appends.ShouldBe(1);
        host.Rules.FindEntry("a")!.Version.ShouldBe(2);
        host.Rules.FindEntry("b")!.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Should_leave_the_whole_envelope_unpublished_when_the_persist_is_refused()
    {
        // Arrange — refuse the very first append
        var store = new RefusingRuleStore(refuseAfter: 0);
        var host = Harness(store);
        var envelope = Envelope(host.Governance, ("a", Document), ("b", Document));

        // Act
        var result = await host.Governance.PublishAsync(envelope.Id, breakGlassActive: false);

        // Assert — nothing live, and no exception: a conflict is an expected outcome
        result.Outcome.ShouldBe(ChangeRequestOutcome.VersionConflict);
        host.Rules.FindEntry("a")!.Version.ShouldBe(1);
        host.Rules.FindEntry("b")!.Version.ShouldBe(1);
        host.Rules.FindEntry("a")!.DocumentJson.ShouldBeNull();
    }

    [Fact]
    public async Task Should_stamp_the_change_request_as_the_approval_reference()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        var host = Harness(store);
        var envelope = Envelope(host.Governance, ("a", Document));

        // Act
        await host.Governance.PublishAsync(envelope.Id, breakGlassActive: false);

        // Assert — the audit trail must connect the row to the request that authorised it
        var history = await store.HistoryAsync("a", default);
        history.ShouldHaveSingleItem();
        history[0].ApprovalRef!.ShouldBe(envelope.Id.ToString());
        history[0].Author.ShouldBe(envelope.Author);
    }

    /// <summary>
    /// The Critical regression this test guards: <c>ChangeRequestPublisher.ApplyValidatedAsync</c>'s
    /// apply phase commits every prepared change from two sources — a rule's own explicit publication,
    /// and any dependent-closure rebind folded into a co-edited proposition's <c>Commits</c>. When a
    /// rule already references a proposition the same envelope also updates, *both* sources touch that
    /// rule's live state: its own publication (the intended, envelope-authored edit) and the
    /// proposition's closure rebind (built from the rule's *pre-envelope* snapshot, since nothing
    /// commits until the apply phase runs). Whichever commits last wins. If rules commit before
    /// propositions, the stale closure rebind runs last and silently reverts the rule's own publish —
    /// live state diverges from what the store durably holds and what the result reports.
    /// </summary>
    [Fact]
    public async Task Should_keep_a_rules_own_publish_when_a_co_edited_propositions_closure_also_rebinds_it()
    {
        // Arrange — rule "a" is redirected to reference a proposition, live, outside the envelope,
        // so the proposition's dependent closure will include rule "a" when the envelope touches it.
        var ruleStore = new InMemoryRuleStore();
        var host = Harness(ruleStore);

        (await host.Propositions.CreateAsync("customer.eligible", "customer", Document, null))
            .Outcome.ShouldBe(PropositionUpdateOutcome.Created);

        const string ruleReferencesEligible = """{ "rule": { "spec": "customer.eligible" } }""";
        (await host.Rules.UpdateAsync("a", ruleReferencesEligible, 1, new RuleChangeProvenance("setup")))
            .Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        host.Rules.FindEntry("a")!.Version.ShouldBe(2);

        // The envelope: both the proposition rule "a" references, and rule "a" itself, are edited
        // together. Rule "a"'s new document is textually different from its current one (so its own
        // publish is a real, distinguishable edit) and still references the proposition (so it would
        // keep binding either way).
        const string eligibleRedefined = """{ "rule": { "not": { "not": { "spec": "customer.is-active" } } } }""";
        const string ruleRedefined = """{ "rule": { "not": { "not": { "spec": "customer.eligible" } } } }""";

        var created = host.Governance.Create("alice", "coordinated edit",
        [
            new(ChangeTargetKind.Proposition, "customer.eligible", eligibleRedefined,
                BaseVersion: 1, RollbackOfVersion: null),
            new(ChangeTargetKind.Rule, "a", ruleRedefined, BaseVersion: 2, RollbackOfVersion: null)
        ]);
        created.Outcome.ShouldBe(ChangeRequestOutcome.Ok);

        // Act
        var published = await host.Governance.PublishAsync(created.Change!.Id, breakGlassActive: false);

        // Assert — the envelope's own edit to rule "a" must be what live state reflects: version 3
        // with the new document, not version 2 with the old one from a stale closure rebind.
        published.Outcome.ShouldBe(ChangeRequestOutcome.Ok);
        published.PublishedVersions!["a"].ShouldBe(3);

        var entry = host.Rules.FindEntry("a")!;
        entry.Version.ShouldBe(3);
        entry.DocumentJson!.ShouldBe(ruleRedefined);

        // The store must agree with live state — no silent divergence between what was durably
        // recorded and what the process is actually evaluating.
        var history = await ruleStore.HistoryAsync("a", default);
        history.Last().Version.ShouldBe(3);
        history.Last().DocumentJson!.ShouldBe(ruleRedefined);
    }

    [Fact]
    public async Task Should_write_one_batch_for_a_propositions_only_envelope()
    {
        // Arrange — three propositions in one change request must persist as one store call
        var ruleStore = new InMemoryRuleStore();
        var propositionStore = new CountingPropositionStore();
        var host = Harness(ruleStore, propositionStore);

        var created = host.Governance.Create("alice", "three propositions",
        [
            new(ChangeTargetKind.Proposition, "customer.p1", Document, BaseVersion: 0, RollbackOfVersion: null, ModelTypeId: "customer"),
            new(ChangeTargetKind.Proposition, "customer.p2", Document, BaseVersion: 0, RollbackOfVersion: null, ModelTypeId: "customer"),
            new(ChangeTargetKind.Proposition, "customer.p3", Document, BaseVersion: 0, RollbackOfVersion: null, ModelTypeId: "customer")
        ]);
        created.Outcome.ShouldBe(ChangeRequestOutcome.Ok);

        // Act
        var result = await host.Governance.PublishAsync(created.Change!.Id, breakGlassActive: false);

        // Assert
        result.Outcome.ShouldBe(ChangeRequestOutcome.Ok);
        propositionStore.Writes.ShouldBe(1);
        host.Propositions.DocumentJsonOf("customer.p1").ShouldNotBeNull();
        host.Propositions.DocumentJsonOf("customer.p2").ShouldNotBeNull();
        host.Propositions.DocumentJsonOf("customer.p3").ShouldNotBeNull();
    }

    /// <summary>The brief's headline case: three propositions, the third conflicts, the first two must not be live.</summary>
    [Fact]
    public async Task Should_leave_all_three_propositions_unpublished_when_the_third_conflicts()
    {
        // Arrange — "customer.p3" already exists at version 1, so authoring against version 0 is stale
        var ruleStore = new InMemoryRuleStore();
        var propositionStore = new CountingPropositionStore();
        var host = Harness(ruleStore, propositionStore);
        (await host.Propositions.CreateAsync("customer.p3", "customer", Document, null))
            .Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        var writesBeforePublish = propositionStore.Writes;

        var created = host.Governance.Create("alice", "three propositions, one stale",
        [
            new(ChangeTargetKind.Proposition, "customer.p1", Document, BaseVersion: 0, RollbackOfVersion: null, ModelTypeId: "customer"),
            new(ChangeTargetKind.Proposition, "customer.p2", Document, BaseVersion: 0, RollbackOfVersion: null, ModelTypeId: "customer"),
            new(ChangeTargetKind.Proposition, "customer.p3", Document, BaseVersion: 0, RollbackOfVersion: null)
        ]);
        created.Outcome.ShouldBe(ChangeRequestOutcome.Ok);

        // Act
        var result = await host.Governance.PublishAsync(created.Change!.Id, breakGlassActive: false);

        // Assert — refused before persistence (Validate catches the stale version before Prepare
        // even runs), so the publish itself never touches the store
        result.Outcome.ShouldBe(ChangeRequestOutcome.VersionConflict);
        propositionStore.Writes.ShouldBe(writesBeforePublish);
        host.Propositions.DocumentJsonOf("customer.p1").ShouldBeNull();
        host.Propositions.DocumentJsonOf("customer.p2").ShouldBeNull();
    }

    /// <summary>
    /// A mixed envelope — proposition create, update and withdraw, plus a rule revert — must still
    /// batch the proposition half as one call, and that call must never happen at all when the rule
    /// half is what conflicts.
    /// </summary>
    [Fact]
    public async Task Should_never_touch_the_proposition_store_for_a_mixed_envelope_when_the_rule_half_conflicts()
    {
        // Arrange
        var ruleStore = new RefusingRuleStore(refuseAfter: 0);
        var propositionStore = new CountingPropositionStore();
        var host = Harness(ruleStore, propositionStore);

        (await host.Propositions.CreateAsync("customer.eligible", "customer", Document, null))
            .Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        (await host.Propositions.CreateAsync("customer.retiring", "customer", Document, null))
            .Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        var writesBeforePublish = propositionStore.Writes;

        var created = host.Governance.Create("alice", "mixed envelope",
        [
            new(ChangeTargetKind.Proposition, "customer.new", Document, BaseVersion: 0, RollbackOfVersion: null, ModelTypeId: "customer"),
            new(ChangeTargetKind.Proposition, "customer.eligible", Document, BaseVersion: 1, RollbackOfVersion: null),
            new(ChangeTargetKind.Proposition, "customer.retiring", null, BaseVersion: 1, RollbackOfVersion: null),
            new(ChangeTargetKind.Rule, "a", null, BaseVersion: 1, RollbackOfVersion: null)
        ]);
        created.Outcome.ShouldBe(ChangeRequestOutcome.Ok);

        // Act
        var result = await host.Governance.PublishAsync(created.Change!.Id, breakGlassActive: false);

        // Assert — the rule half's refusal must stop phase 2 before the proposition batch is ever sent
        result.Outcome.ShouldBe(ChangeRequestOutcome.VersionConflict);
        propositionStore.Writes.ShouldBe(writesBeforePublish);
        host.Propositions.DocumentJsonOf("customer.new").ShouldBeNull();
        host.Propositions.DocumentJsonOf("customer.retiring").ShouldNotBeNull();
        host.Rules.FindEntry("a")!.Version.ShouldBe(1);
    }

    /// <summary>
    /// Important B's scenario: the rule half durably persists, then the proposition half's store call
    /// throws (an infrastructure fault, not a business refusal — <see cref="IPropositionStore.WriteAsync"/>
    /// has no conflict result to return instead). Nothing must be live, but the caller must be told
    /// this request cannot simply be retried — see <see cref="ChangeRequestOutcome.PersistenceDesynced"/>.
    /// </summary>
    [Fact]
    public async Task Should_report_persistence_desynced_when_the_proposition_store_fails_after_the_rule_half_persists()
    {
        // Arrange
        var ruleStore = new InMemoryRuleStore();
        var propositionStore = new ThrowingPropositionStore();
        var host = Harness(ruleStore, propositionStore);

        var created = host.Governance.Create("alice", "mixed envelope",
        [
            new(ChangeTargetKind.Rule, "a", Document, BaseVersion: 1, RollbackOfVersion: null),
            new(ChangeTargetKind.Proposition, "customer.p1", Document, BaseVersion: 0, RollbackOfVersion: null, ModelTypeId: "customer")
        ]);
        created.Outcome.ShouldBe(ChangeRequestOutcome.Ok);

        // Act
        var result = await host.Governance.PublishAsync(created.Change!.Id, breakGlassActive: false);

        // Assert — a distinct outcome, not a thrown exception and not an ordinary conflict
        result.Outcome.ShouldBe(ChangeRequestOutcome.PersistenceDesynced);

        // The rule row landed durably even though nothing is live — exactly the divergence a bare
        // retry would collide with.
        var history = await ruleStore.HistoryAsync("a", default);
        history.ShouldHaveSingleItem();
        history[0].Version.ShouldBe(2);
        host.Rules.FindEntry("a")!.Version.ShouldBe(1);
        host.Propositions.DocumentJsonOf("customer.p1").ShouldBeNull();
    }

    /// <summary>
    /// The order-dependent proposition-to-proposition variant of the Critical apply-order defect.
    /// "customer.p2" references "customer.p1"; both are updated in the same envelope, with p2 listed
    /// *before* p1. p1's own phase-A prepare walks its dependent closure and finds p2 live there
    /// (p2's *old* document references p1) — without excluding p2 (also independently, explicitly
    /// updated in this same envelope) from that walk, it would rebind p2 from p2's stale document and
    /// fold that rebind into the live overlay when p1 commits, landing *after* p2's own fresh commit
    /// and clobbering it — even though <see cref="PropositionSet.DocumentJsonOf"/> and the reported
    /// version both correctly show p2's new state, because those read the generation's
    /// <c>Authored</c> table, not its <c>Overlay</c>.
    /// </summary>
    [Fact]
    public async Task Should_not_let_an_updated_propositions_own_publish_be_clobbered_by_a_co_updated_dependencys_stale_closure_rebind()
    {
        // Arrange — "customer.also-active" is a second compiled spec with the same truth value as
        // "customer.is-active", so p1's edit is a genuine, distinguishable document change without
        // changing p1's *value* for the test customer below — isolating the clobber from the
        // separate, already-accepted "phase A binds in envelope order, not dependency order"
        // limitation, which would otherwise also perturb the result and mask what this test targets.
        var registry = new SpecRegistry()
            .Register("customer.is-active", IsActive)
            .Register("customer.also-active", Spec.Build((Customer c) => c.IsActive).Create("also active"));
        var scope = new BindingScope(registry);
        var propositions = new PropositionSet(scope, new InMemoryPropositionStore()).AddModel<Customer>("customer");
        var rules = new RuleSet(scope, new InMemoryRuleStore());
        var governance = new ChangeRequestSet(new ApprovalGate(), rules, propositions);

        (await propositions.CreateAsync(
            "customer.p1", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null))
            .Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        (await propositions.CreateAsync(
            "customer.p2", "customer", """{ "rule": { "spec": "customer.p1" } }""", null))
            .Outcome.ShouldBe(PropositionUpdateOutcome.Created);

        // p2's edit inverts what it resolves through p1 (still true, for the active customer below,
        // when correctly bound early against p1's *old* value). p1's edit swaps to the same-valued
        // second spec — a real, distinguishable document change that leaves p1's value unchanged, so
        // only the clobber (which reads p1's *new* prospective value through p2's *stale*, un-inverted
        // document) can make the two paths disagree.
        const string p2Inverted = """{ "rule": { "not": { "spec": "customer.p1" } } }""";
        const string p1SameValueNewDocument = """{ "rule": { "spec": "customer.also-active" } }""";

        var created = governance.Create("alice", "coordinated proposition edit",
        [
            // p2 listed before p1 — the order the reported bug needs.
            new(ChangeTargetKind.Proposition, "customer.p2", p2Inverted, BaseVersion: 1, RollbackOfVersion: null),
            new(ChangeTargetKind.Proposition, "customer.p1", p1SameValueNewDocument, BaseVersion: 1, RollbackOfVersion: null)
        ]);
        created.Outcome.ShouldBe(ChangeRequestOutcome.Ok);

        // Act
        var published = await governance.PublishAsync(created.Change!.Id, breakGlassActive: false);

        // Assert — the reported state says success either way; only fresh evaluation off Scope.Source
        // reveals whether the live overlay actually holds p2's new definition.
        published.Outcome.ShouldBe(ChangeRequestOutcome.Ok);
        published.PublishedVersions!["customer.p2"].ShouldBe(2);
        propositions.DocumentJsonOf("customer.p2")!.ShouldBe(p2Inverted);

        var check = new CheckRule();
        rules.Add(check);
        var activeCustomer = new Customer(IsActive: true, Age: 0);

        // Correct: p2 binds early (phase A), before p1's own edit lands, against p1's *old* value
        // (true) — p2_new = NOT true = false. A stale p2 (the clobber: rebound from p2's *old*,
        // un-inverted document, "spec": "customer.p1", against p1's *new* prospective value — also
        // true, since customer.also-active mirrors customer.is-active) would instead evaluate true.
        check.Evaluate(activeCustomer).Satisfied.ShouldBeFalse();
    }

    /// <summary>
    /// The order-independent variant: no envelope authoring order avoids this one. "customer.p3" has
    /// a compiled spec beneath an authored override; "customer.p2" references it. The envelope
    /// updates p2 and withdraws p3. Phase 3 structurally commits every publish before every
    /// withdrawal, so p3's withdrawal-closure rebind of p2 always runs *after* p2's own publish
    /// commit, regardless of which order p2/p3 are listed — proving the fix must be exclusion, not
    /// ordering.
    /// </summary>
    [Fact]
    public async Task Should_not_let_an_updated_propositions_own_publish_be_clobbered_by_a_co_withdrawn_dependencys_closure_rebind()
    {
        // Arrange — a compiled spec named "customer.p3" sits beneath the authored override
        var registry = new SpecRegistry()
            .Register("customer.is-active", IsActive)
            .Register("customer.p3", IsActive);
        var scope = new BindingScope(registry);
        var propositions = new PropositionSet(scope, new InMemoryPropositionStore()).AddModel<Customer>("customer");
        var rules = new RuleSet(scope, new InMemoryRuleStore());
        var governance = new ChangeRequestSet(new ApprovalGate(), rules, propositions);

        (await propositions.CreateAsync(
            "customer.p3", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null))
            .Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        (await propositions.CreateAsync(
            "customer.p2", "customer", """{ "rule": { "spec": "customer.p3" } }""", null))
            .Outcome.ShouldBe(PropositionUpdateOutcome.Created);

        const string p2Inverted = """{ "rule": { "not": { "spec": "customer.p3" } } }""";

        var created = governance.Create("alice", "update-and-withdraw",
        [
            new(ChangeTargetKind.Proposition, "customer.p2", p2Inverted, BaseVersion: 1, RollbackOfVersion: null),
            new(ChangeTargetKind.Proposition, "customer.p3", null, BaseVersion: 1, RollbackOfVersion: null)
        ]);
        created.Outcome.ShouldBe(ChangeRequestOutcome.Ok);

        // Act
        var published = await governance.PublishAsync(created.Change!.Id, breakGlassActive: false);

        // Assert
        published.Outcome.ShouldBe(ChangeRequestOutcome.Ok);
        published.PublishedVersions!["customer.p2"].ShouldBe(2);
        propositions.DocumentJsonOf("customer.p2")!.ShouldBe(p2Inverted);

        var check = new CheckRule();
        rules.Add(check);
        var activeCustomer = new Customer(IsActive: true, Age: 0);

        // Correct: p3 reverts to its compiled default (is-active = true); p2_new = NOT p3 = false. A
        // stale p2 (rebound from its *old* document, "spec": "customer.p3") would instead mirror p3
        // directly and evaluate true here.
        check.Evaluate(activeCustomer).Satisfied.ShouldBeFalse();
    }

    /// <summary>
    /// The capability the exclusion set delivers, not just the bug it fixes: a coordinated repair —
    /// a proposition edit that would break a dependent rule's *current* document, published together
    /// with the rule edit that accommodates it — now succeeds as one envelope. Before <c>EnvelopeNodes</c>,
    /// phase A's dependent-closure check rebound the rule from its live document (still referencing
    /// the proposition about to change) and refused, naming the very rule the envelope repairs — the
    /// coordinated edit had to be split into two change requests, the rule's first. See
    /// <c>ChangeRequestPublisher.Validate</c>'s remarks, rewritten in the same commit as this test to
    /// describe this, not the old refusal.
    /// </summary>
    [Fact]
    public async Task Should_publish_a_proposition_edit_together_with_the_rule_edit_that_accommodates_it()
    {
        // Arrange — a policy-flavoured rule bound, by document, through a proposition. Editing the
        // proposition alone to a non-policy spec is exactly Should_return_a_broken_dependent_cascade_as_a_value_rather_than_throwing
        // in ChangeRequestSetTests — refused, because the rule's live document still resolves through
        // it. Here the same envelope also redirects the rule straight to the still-policy spec.
        var registry = new SpecRegistry()
            .Register("customer.is-active-policy", IsActivePolicy)
            .Register("customer.composed", ComposedNonPolicy);
        var scope = new BindingScope(registry);
        var propositions = new PropositionSet(scope, new InMemoryPropositionStore()).AddModel<Customer>("customer");
        (await propositions.CreateAsync("customer.eligible-policy", "customer",
            """{ "rule": { "spec": "customer.is-active-policy" } }""", null))
            .Outcome.ShouldBe(PropositionUpdateOutcome.Created);

        var rule = new CanCheckoutPolicyRule();
        var rules = new RuleSet(scope).Add(rule);
        (await rules.UpdateAsync(
            "can-checkout-policy", """{ "rule": { "spec": "customer.eligible-policy" } }""", 1,
            new RuleChangeProvenance("setup")))
            .Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        var governance = new ChangeRequestSet(new ApprovalGate(), rules, propositions);

        const string propositionTurnsNonPolicy = """{ "rule": { "spec": "customer.composed" } }""";
        const string ruleRedirectedAroundIt = """{ "rule": { "spec": "customer.is-active-policy" } }""";

        var created = governance.Create("alice", "coordinated repair",
        [
            new(ChangeTargetKind.Proposition, "customer.eligible-policy", propositionTurnsNonPolicy,
                BaseVersion: 1, RollbackOfVersion: null),
            new(ChangeTargetKind.Rule, "can-checkout-policy", ruleRedirectedAroundIt,
                BaseVersion: 2, RollbackOfVersion: null)
        ]);
        created.Outcome.ShouldBe(ChangeRequestOutcome.Ok);

        // Act
        var published = await governance.PublishAsync(created.Change!.Id, breakGlassActive: false);

        // Assert — both land, in one envelope, not two
        published.Outcome.ShouldBe(ChangeRequestOutcome.Ok);
        published.PublishedVersions!["customer.eligible-policy"].ShouldBe(2);
        published.PublishedVersions!["can-checkout-policy"].ShouldBe(3);

        // Through Scope.Source, not just the reported state: the proposition genuinely resolves its
        // new, non-policy definition...
        scope.Source.Find("customer.eligible-policy")!.Spec.ShouldNotBeSameAs(IsActivePolicy);

        // ...and the rule genuinely resolves its new document — still policy-flavoured (it would
        // throw PolicyRequired otherwise), still evaluable, and reflecting the redirect rather than
        // the now-broken proposition.
        rule.Evaluate(new Customer(IsActive: true, Age: 30)).Satisfied.ShouldBeTrue();
        rule.Evaluate(new Customer(IsActive: false, Age: 30)).Satisfied.ShouldBeFalse();
    }
}
