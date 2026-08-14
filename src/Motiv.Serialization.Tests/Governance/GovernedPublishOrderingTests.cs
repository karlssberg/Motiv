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

    private sealed class ARule() : Rule<Customer, string>("a", IsActive);

    private sealed class BRule() : Rule<Customer, string>("b", IsActive);

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

    private sealed record Host(ChangeRequestSet Governance, RuleSet Rules);

    private static Host Harness(IRuleStore store)
    {
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var scope = new BindingScope(registry);
        var propositions = new PropositionSet(scope, new InMemoryPropositionStore()).AddModel<Customer>("customer");
        var rules = new RuleSet(scope, store).Add(new ARule()).Add(new BRule());
        var gate = new ApprovalGate();
        return new Host(new ChangeRequestSet(gate, rules, propositions), rules);
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
}
