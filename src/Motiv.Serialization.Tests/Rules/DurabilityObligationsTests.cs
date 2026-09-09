using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Rules;

/// <summary>
/// The verification obligations of bundle spec 2 §7 that no earlier test already covers. One test per
/// obligation, so a reviewer can check them off against the spec.
/// </summary>
public class DurabilityObligationsTests
{
    // Plain class (not a record) so the net472 target compiles without an IsExternalInit polyfill.
    private sealed class Customer(bool isActive)
    {
        public bool IsActive { get; } = isActive;
    }

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class SampleRule() : Rule<Customer, string>("sample", IsActive);

    private const string Document = """{ "rule": { "spec": "customer.is-active" } }""";

    /// <summary>A second document that binds, so a replaced row is distinguishable from the original.</summary>
    private const string Replacement = """{ "rule": { "not": { "spec": "customer.is-active" } } }""";

    /// <summary>A RuleSet over the given store; two of them over one store are two replicas.</summary>
    private static RuleSet Replica(IRuleStore store)
    {
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var set = new RuleSet(registry, store).Add(new SampleRule());
        set.Load();
        return set;
    }

    /// <summary>
    /// A PropositionSet over the given store; two of them over one store are two replicas, exactly as
    /// two <see cref="RuleSet"/>s are. Each gets its own <see cref="BindingScope"/>, because a shared
    /// scope would be one replica with two handles — and the outer gate would then serialise the very
    /// race these tests exist to run.
    /// </summary>
    private static PropositionSet PropositionReplica(IPropositionStore store)
    {
        var set = new PropositionSet(new SpecRegistry().Register("customer.is-active", IsActive), store)
            .AddModel<Customer>("customer");
        set.Load();
        return set;
    }

    /// <summary>Delegates to an in-memory store, recording which names it was asked to write.</summary>
    private sealed class RecordingRuleStore(List<string> written) : IRuleStore
    {
        private readonly InMemoryRuleStore _inner = new();

        public IReadOnlyList<StoredRule> Load() => _inner.Load();
        public Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken ct) => _inner.LoadAsync(ct);
        public Task<long> GetGenerationAsync(CancellationToken ct) => _inner.GetGenerationAsync(ct);
        public Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(string name, CancellationToken ct) =>
            _inner.HistoryAsync(name, ct);

        public Task<RuleAppendResult> AppendAsync(
            IReadOnlyList<StoredRuleVersion> versions, CancellationToken ct)
        {
            written.AddRange(versions.Select(row => row.Name));
            return _inner.AppendAsync(versions, ct);
        }
    }

    /// <summary>The proposition-side twin of <see cref="RecordingRuleStore"/>.</summary>
    private sealed class RecordingPropositionStore(List<string> written) : IPropositionStore
    {
        private readonly InMemoryPropositionStore _inner = new();

        public IReadOnlyList<StoredProposition> Load() => _inner.Load();
        public Task<IReadOnlyList<StoredProposition>> LoadAsync(CancellationToken ct) => _inner.LoadAsync(ct);
        public Task<long> GetGenerationAsync(CancellationToken ct) => _inner.GetGenerationAsync(ct);

        public Task<PropositionWriteResult> WriteAsync(PropositionBatch batch, CancellationToken ct)
        {
            written.AddRange(batch.Saves.Select(p => p.Name));
            written.AddRange(batch.Deletes.Select(deletion => deletion.Name));
            return _inner.WriteAsync(batch, ct);
        }
    }

    [Fact]
    public async Task Should_publish_once_and_reject_once_when_two_replicas_race_a_write()
    {
        // Arrange — separate RuleSets, one shared store: separate outer gates, one primary key
        var store = new InMemoryRuleStore();
        var a = Replica(store);
        var b = Replica(store);

        // Act — both hold baseVersion 1, and neither gate can see the other
        var results = await Task.WhenAll(
            a.UpdateAsync("sample", Document, 1, new RuleChangeProvenance("alice")),
            b.UpdateAsync("sample", Document, 1, new RuleChangeProvenance("bob")));

        // Assert — the lost update is impossible: the PK, not a lock, is what decides
        results.Count(r => r.Outcome == RuleUpdateOutcome.Updated).ShouldBe(1);
        results.Count(r => r.Outcome == RuleUpdateOutcome.VersionConflict).ShouldBe(1);

        // ...and the audit shows exactly one published version, not two
        var history = await store.HistoryAsync("sample", default);
        history.ShouldHaveSingleItem();
        history[0].Version.ShouldBe(2);
    }

    [Fact]
    public async Task Should_leave_nothing_live_in_the_losing_replica()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        var a = Replica(store);
        var b = Replica(store);

        await a.UpdateAsync("sample", Document, 1, new RuleChangeProvenance("alice"));

        // Act — b is now stale and does not know it
        var result = await b.UpdateAsync("sample", Document, 1, new RuleChangeProvenance("bob"));

        // Assert — the refusal must reach memory too, or b would run behaviour the log never recorded
        result.Outcome.ShouldBe(RuleUpdateOutcome.VersionConflict);
        b.FindEntry("sample")!.Version.ShouldBe(1);
        b.FindEntry("sample")!.DocumentJson.ShouldBeNull();
    }

    [Fact]
    public async Task Should_report_the_current_version_when_the_base_version_is_stale()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        var set = Replica(store);
        await set.UpdateAsync("sample", Document, 1, new RuleChangeProvenance("alice"));

        // Act — an editor whose tab sat open through someone else's save
        var result = await set.UpdateAsync("sample", Document, 1, new RuleChangeProvenance("bob"));

        // Assert — the refusal must carry the version to re-base onto, or the editor cannot recover
        result.Outcome.ShouldBe(RuleUpdateOutcome.VersionConflict);
        result.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Should_publish_once_and_reject_once_when_two_replicas_race_a_proposition_write()
    {
        // Arrange — the proposition-side twin of the rule obligation above. Separate PropositionSets,
        // one shared store: separate outer gates, one version compare-and-set.
        var store = new InMemoryPropositionStore();
        var a = PropositionReplica(store);
        await a.CreateAsync("customer.p", "customer", Document, null);

        // Built after the row exists: Load() is the startup read and runs once, so a second replica
        // at a known basis is constructed at that moment rather than re-reading.
        var b = PropositionReplica(store);

        // Act — both hold version 1, and neither gate can see the other
        var results = await Task.WhenAll(
            a.UpdateAsync("customer.p", Document, 1),
            b.UpdateAsync("customer.p", Document, 1));

        // Assert — the lost update is impossible. Before this slice both would have returned Updated:
        // each replica's expectedVersion check compared against its own memory, which is silent about
        // the other.
        results.Count(r => r.Outcome == PropositionUpdateOutcome.Updated).ShouldBe(1);
        results.Count(r => r.Outcome == PropositionUpdateOutcome.VersionConflict).ShouldBe(1);

        // ...and the store holds exactly one published version, not two writes over each other
        store.Load().ShouldHaveSingleItem();
        store.Load()[0].Version.ShouldBe(2);
    }

    [Fact]
    public async Task Should_report_the_current_proposition_version_when_the_base_version_is_stale()
    {
        // Arrange
        var store = new InMemoryPropositionStore();
        var a = PropositionReplica(store);
        await a.CreateAsync("customer.p", "customer", Document, null);

        // Built after the row exists: Load() is the startup read and runs once, so a second replica
        // at a known basis is constructed at that moment rather than re-reading.
        var b = PropositionReplica(store);
        await a.UpdateAsync("customer.p", Document, 1);

        // Act — b is now stale and does not know it: its own memory still says version 1, so its
        // in-process check passes and only the store can refuse this.
        var result = await b.UpdateAsync("customer.p", Document, 1);

        // Assert — the refusal must carry the version to re-base onto, or the editor cannot recover
        result.Outcome.ShouldBe(PropositionUpdateOutcome.VersionConflict);
        result.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Should_leave_nothing_live_in_the_replica_that_loses_a_proposition_race()
    {
        // Arrange
        var store = new InMemoryPropositionStore();
        var a = PropositionReplica(store);
        await a.CreateAsync("customer.p", "customer", Document, null);

        // Built after the row exists: Load() is the startup read and runs once, so a second replica
        // at a known basis is constructed at that moment rather than re-reading.
        var b = PropositionReplica(store);
        await a.UpdateAsync("customer.p", Replacement, 1);

        // Act
        var result = await b.UpdateAsync("customer.p", Replacement, 1);

        // Assert — a refused publish leaves nothing live, so b must still be on what it loaded, and
        // the store must still hold a's document rather than b's
        result.Outcome.ShouldBe(PropositionUpdateOutcome.VersionConflict);
        b.Find("customer.p")!.Version.ShouldBe(1);
        store.Load().ShouldHaveSingleItem();
        store.Load()[0].DocumentJson.ShouldBe(Replacement);
    }

    [Fact]
    public async Task Should_refuse_a_withdrawal_whose_version_another_replica_moved_past()
    {
        // Arrange — a withdrawal names an existing position rather than claiming a new one, so its
        // compare-and-set is equality, not "past the head". It still has to be one.
        var store = new InMemoryPropositionStore();
        var a = PropositionReplica(store);
        await a.CreateAsync("customer.p", "customer", Document, null);

        // Built after the row exists: Load() is the startup read and runs once, so a second replica
        // at a known basis is constructed at that moment rather than re-reading.
        var b = PropositionReplica(store);
        await a.UpdateAsync("customer.p", Replacement, 1);

        // Act — b withdraws the version it last saw, which a has already replaced
        var result = await b.WithdrawAsync("customer.p", 1);

        // Assert — the row a published must survive
        result.Outcome.ShouldBe(PropositionUpdateOutcome.VersionConflict);
        result.Version.ShouldBe(2);
        store.Load().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Should_never_write_the_two_stores_together()
    {
        // Arrange — one scope, two stores, each recording what it was asked to write
        var ruleWrites = new List<string>();
        var propositionWrites = new List<string>();

        var scope = new BindingScope(new SpecRegistry().Register("customer.is-active", IsActive));
        var propositions = new PropositionSet(scope, new RecordingPropositionStore(propositionWrites))
            .AddModel<Customer>("customer");
        propositions.Load();

        var rules = new RuleSet(scope, new RecordingRuleStore(ruleWrites)).Add(new SampleRule());
        rules.Load();

        // Act
        await propositions.CreateAsync("customer.a", "customer", Document, null);
        await rules.UpdateAsync("sample", Document, 1, new RuleChangeProvenance("alice"));

        // Assert — each store saw only its own write; no operation spans both
        propositionWrites.ShouldBe(["customer.a"]);
        ruleWrites.ShouldBe(["sample"]);
    }
}
