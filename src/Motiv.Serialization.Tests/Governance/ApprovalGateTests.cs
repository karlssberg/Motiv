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

    /// <summary>In-memory fake store, sufficient to prove a second <see cref="ApprovalGate"/> instance reloads it.</summary>
    private sealed class FakeGateStore : IGateStore
    {
        private string? _documentJson;

        public string? Load() => _documentJson;

        public void Save(string? documentJson) => _documentJson = documentJson;
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
}
