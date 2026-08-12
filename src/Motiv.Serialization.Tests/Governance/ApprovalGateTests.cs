namespace Motiv.Serialization.Tests.Governance;

public class ApprovalGateTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private const string MakerCheckerDocument =
        """
        {"rule": {"and": [
            {"spec": "change.approver-count-at-least", "args": {"n": 1}},
            {"not": {"spec": "change.author-is-approver"}}
        ]}}
        """;

    private static ProposedChange AProposedChange() =>
        new(
            new ChangeTarget(ChangeTargetKind.Proposition, "pricing.eu.vat"),
            "{}",
            BaseVersion: 1,
            new ChangeClassification(
                IsCreation: false,
                IsDeletion: false,
                IsMetadataOnly: false,
                TouchesAsyncSpec: false,
                IsRollback: false,
                RollbackOfVersion: null));

    private static ChangeRequest Request(
        string author = "alice",
        IReadOnlyList<Approval>? approvals = null)
    {
        var request = new ChangeRequest(Guid.NewGuid(), author, "a change", [AProposedChange()]);
        foreach (var approval in approvals ?? [])
            request.AddApproval(approval);
        return request;
    }

    /// <summary>
    /// In-memory fake store, sufficient to prove a second <see cref="ApprovalGate"/> instance
    /// reloads it. <see cref="ThrowOnSave"/> lets a test simulate a persistence failure on demand.
    /// </summary>
    private sealed class FakeGateStore : IGateStore
    {
        private string? _documentJson;

        public bool ThrowOnSave { get; set; }

        public string? Load() => _documentJson;

        public void Save(string? documentJson)
        {
            if (ThrowOnSave)
                throw new IOException("simulated store failure");

            _documentJson = documentJson;
        }
    }

    [Fact]
    public void Should_allow_anything_by_default_when_no_gate_is_configured()
    {
        // Arrange
        var gate = new ApprovalGate();

        // Act
        var decision = gate.Evaluate(Request());

        // Assert
        decision.MayPublish.ShouldBeTrue();
        decision.Reason.ShouldBe("no approval gate is configured");
        decision.Assertions.ShouldBe(["no approval gate is configured"]);
        decision.Justification.ShouldBe("no approval gate is configured");
    }

    [Fact]
    public void Should_block_an_unapproved_change_once_a_maker_checker_gate_is_set()
    {
        // Arrange
        var gate = new ApprovalGate();
        var updateResult = gate.SetGate(MakerCheckerDocument, []);
        var unapproved = Request(author: "alice");

        // Act
        var decision = gate.Evaluate(unapproved);

        // Assert
        updateResult.Outcome.ShouldBe(GateUpdateOutcome.Updated);
        decision.MayPublish.ShouldBeFalse();
        decision.Assertions.ShouldContain("change has fewer than 1 approvals");
    }

    [Fact]
    public void Should_allow_a_peer_approved_change_once_a_maker_checker_gate_is_set()
    {
        // Arrange
        var gate = new ApprovalGate();
        gate.SetGate(MakerCheckerDocument, []);
        var peerApproved = Request(author: "alice", approvals: [new Approval("bob", Now, [])]);

        // Act
        var decision = gate.Evaluate(peerApproved);

        // Assert
        decision.MayPublish.ShouldBeTrue();
    }

    [Fact]
    public void Should_reject_invalid_json_and_leave_the_gate_unchanged()
    {
        // Arrange
        var gate = new ApprovalGate();
        gate.SetGate(MakerCheckerDocument, []);
        var documentBeforeAttempt = gate.DocumentJson;

        // Act
        var updateResult = gate.SetGate("{not valid json", []);

        // Assert
        updateResult.Outcome.ShouldBe(GateUpdateOutcome.Invalid);
        updateResult.Errors.ShouldNotBeEmpty();
        gate.DocumentJson!.ShouldBe(documentBeforeAttempt!);
    }

    [Fact]
    public void Should_reset_to_permissive_when_gate_is_set_to_null()
    {
        // Arrange
        var gate = new ApprovalGate();
        gate.SetGate(MakerCheckerDocument, []);

        // Act
        var updateResult = gate.SetGate(null, []);
        var decision = gate.Evaluate(Request());

        // Assert
        updateResult.Outcome.ShouldBe(GateUpdateOutcome.Updated);
        gate.DocumentJson.ShouldBeNull();
        decision.MayPublish.ShouldBeTrue();
        decision.Reason.ShouldBe("no approval gate is configured");
    }

    [Fact]
    public void Should_reload_its_document_from_the_store_in_a_second_instance()
    {
        // Arrange
        var store = new FakeGateStore();
        var firstGate = new ApprovalGate(store);
        firstGate.SetGate(MakerCheckerDocument, []);

        // Act
        var secondGate = new ApprovalGate(store);
        var decision = secondGate.Evaluate(Request(author: "alice"));

        // Assert
        secondGate.DocumentJson!.ShouldBe(MakerCheckerDocument);
        decision.MayPublish.ShouldBeFalse();
        decision.Assertions.ShouldContain("change has fewer than 1 approvals");
    }

    [Fact]
    public void Should_propagate_a_store_save_failure_and_leave_the_gate_unchanged()
    {
        // Arrange — a gate already holding a maker-checker document, backed by a store that now fails to persist
        var store = new FakeGateStore();
        var gate = new ApprovalGate(store);
        gate.SetGate(MakerCheckerDocument, []);
        store.ThrowOnSave = true;

        // Act — attempt to reset to permissive; the store rejects the write
        Should.Throw<IOException>(() => gate.SetGate(null, []));

        // Assert — the exception propagated, and the in-memory gate never swapped: it still
        // behaves exactly as the maker-checker document it had before the failed attempt
        gate.DocumentJson!.ShouldBe(MakerCheckerDocument);
        gate.Evaluate(Request(author: "alice")).MayPublish.ShouldBeFalse();
        gate.Evaluate(Request(author: "alice", approvals: [new Approval("bob", Now, [])])).MayPublish.ShouldBeTrue();
    }

    [Fact]
    public void Should_throw_when_the_stored_document_is_invalid()
    {
        // Arrange — the store is a dumb sink: it can hold a document that never passed SetGate's
        // own validation, e.g. because it was tampered with or corrupted at rest.
        var store = new FakeGateStore();
        store.Save("{not valid json");

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => new ApprovalGate(store));

        // Assert — fails closed and loud: names the store as the source, and surfaces the
        // underlying error detail rather than silently starting up permissive.
        exception.Message.ShouldContain("IGateStore");
        exception.Message.ShouldContain("invalid JSON");
    }

    /// <summary>
    /// The built-in <see cref="GateSpecs"/> catalogue is all synchronous, so the only way to exercise
    /// the gate's synchronous-only guard is a registry that carries an async entry the built-in
    /// catalogue never has — hence the internal test-only constructor overload.
    /// </summary>
    private static SpecRegistry RegistryWithAnAsyncEntry() =>
        GateSpecs.CreateRegistry().Register(
            "change.async-noop",
            Spec.BuildAsync((ChangeRequest c) => new ValueTask<bool>(true))
                .WhenTrue("async noop true")
                .WhenFalse("async noop false")
                .Create());

    [Fact]
    public void Should_reject_a_gate_document_that_references_an_async_registry_entry()
    {
        // Arrange — a registry extended with a throwaway async spec, and a document referencing it
        var gate = new ApprovalGate(store: null, RegistryWithAnAsyncEntry());

        // Act
        var updateResult = gate.SetGate("""{"rule": {"spec": "change.async-noop"}}""", []);

        // Assert — refused with the gate-specific code, not the binder's generic one, and the gate
        // stays at its permissive default
        updateResult.Outcome.ShouldBe(GateUpdateOutcome.Invalid);
        updateResult.Errors.ShouldContain(error => error.Code == RuleErrorCode.GateMustBeSynchronous);
        gate.DocumentJson.ShouldBeNull();
    }

    [Fact]
    public void Should_accept_a_synchronous_document_through_a_registry_that_also_carries_an_async_entry()
    {
        // Arrange — the same extended registry, but a document that never references the async entry
        var gate = new ApprovalGate(store: null, RegistryWithAnAsyncEntry());

        // Act
        var updateResult = gate.SetGate(MakerCheckerDocument, []);

        // Assert — the guard only refuses documents that actually reference an async entry
        updateResult.Outcome.ShouldBe(GateUpdateOutcome.Updated);
        gate.DocumentJson!.ShouldBe(MakerCheckerDocument);
    }
}
