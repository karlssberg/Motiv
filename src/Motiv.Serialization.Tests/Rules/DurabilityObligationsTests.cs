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

    /// <summary>A RuleSet over the given store; two of them over one store are two replicas.</summary>
    private static RuleSet Replica(IRuleStore store)
    {
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var set = new RuleSet(registry, store).Add(new SampleRule());
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

        public Task WriteAsync(PropositionBatch batch, CancellationToken ct)
        {
            written.AddRange(batch.Saves.Select(p => p.Name));
            written.AddRange(batch.Deletes);
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
