namespace Motiv.Serialization.Tests.Governance;

public class GateSpecsTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static ChangeRequest Request(
        string author = "alice",
        IReadOnlyList<Approval>? approvals = null,
        IReadOnlyList<ProposedChange>? proposedChanges = null)
    {
        var request = new ChangeRequest(
            Guid.NewGuid(), author, "a change", proposedChanges ?? [AProposedChange()]);

        foreach (var approval in approvals ?? [])
            request.AddApproval(approval);

        return request;
    }

    private static ProposedChange AProposedChange(
        ChangeTargetKind kind = ChangeTargetKind.Proposition,
        string name = "pricing.eu.vat",
        bool isCreation = false,
        bool isDeletion = false,
        bool isMetadataOnly = false,
        bool touchesAsyncSpec = false,
        bool isRollback = false) =>
        new(
            new ChangeTarget(kind, name),
            "{}",
            BaseVersion: 1,
            new ChangeClassification(
                IsCreation: isCreation,
                IsDeletion: isDeletion,
                IsMetadataOnly: isMetadataOnly,
                TouchesAsyncSpec: touchesAsyncSpec,
                IsRollback: isRollback,
                RollbackOfVersion: null));

    private static RuleSerializer Serializer() => new(GateSpecs.CreateRegistry());

    // --- change.in-namespace ---

    [Fact]
    public void Should_be_satisfied_when_a_target_falls_under_the_namespace_prefix()
    {
        // Arrange
        var spec = Serializer().Deserialize<ChangeRequest>(
            """{"rule": {"spec": "change.in-namespace", "args": {"prefix": "pricing.eu"}}}""");
        var request = Request(proposedChanges: [AProposedChange(name: "pricing.eu.vat")]);

        // Act
        var result = spec.Evaluate(request);

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["change touches namespace 'pricing.eu'"]);
    }

    [Fact]
    public void Should_be_unsatisfied_when_no_target_falls_under_the_namespace_prefix()
    {
        // Arrange
        var spec = Serializer().Deserialize<ChangeRequest>(
            """{"rule": {"spec": "change.in-namespace", "args": {"prefix": "pricing.eu"}}}""");
        var request = Request(proposedChanges: [AProposedChange(name: "billing.invoice")]);

        // Act
        var result = spec.Evaluate(request);

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Assertions.ShouldBe(["change does not touch namespace 'pricing.eu'"]);
    }

    // --- change.target-is-proposition ---

    [Fact]
    public void Should_be_satisfied_when_a_target_is_a_proposition()
    {
        // Arrange
        var spec = Serializer().Deserialize<ChangeRequest>("""{"rule": {"spec": "change.target-is-proposition"}}""");
        var request = Request(proposedChanges: [AProposedChange(kind: ChangeTargetKind.Proposition)]);

        // Act
        var result = spec.Evaluate(request);

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["change targets a proposition"]);
    }

    [Fact]
    public void Should_be_unsatisfied_when_no_target_is_a_proposition()
    {
        // Arrange
        var spec = Serializer().Deserialize<ChangeRequest>("""{"rule": {"spec": "change.target-is-proposition"}}""");
        var request = Request(proposedChanges: [AProposedChange(kind: ChangeTargetKind.Rule, name: "checkout-eligibility")]);

        // Act
        var result = spec.Evaluate(request);

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Assertions.ShouldBe(["change targets no proposition"]);
    }

    // --- change.approver-count-at-least ---

    [Fact]
    public void Should_be_satisfied_when_approval_count_meets_the_threshold()
    {
        // Arrange
        var spec = Serializer().Deserialize<ChangeRequest>(
            """{"rule": {"spec": "change.approver-count-at-least", "args": {"n": 2}}}""");
        var request = Request(approvals: [new Approval("bob", Now, []), new Approval("carol", Now, [])]);

        // Act
        var result = spec.Evaluate(request);

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["change has at least 2 approvals"]);
    }

    [Fact]
    public void Should_be_unsatisfied_when_approval_count_is_below_the_threshold()
    {
        // Arrange
        var spec = Serializer().Deserialize<ChangeRequest>(
            """{"rule": {"spec": "change.approver-count-at-least", "args": {"n": 2}}}""");
        var request = Request(approvals: [new Approval("bob", Now, [])]);

        // Act
        var result = spec.Evaluate(request);

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Assertions.ShouldBe(["change has fewer than 2 approvals"]);
    }

    // --- change.author-is-approver ---

    [Fact]
    public void Should_be_satisfied_when_the_author_approved_their_own_change()
    {
        // Arrange
        var spec = Serializer().Deserialize<ChangeRequest>("""{"rule": {"spec": "change.author-is-approver"}}""");
        var request = Request(author: "alice", approvals: [new Approval("alice", Now, [])]);

        // Act
        var result = spec.Evaluate(request);

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["the author approved their own change"]);
    }

    [Fact]
    public void Should_be_unsatisfied_when_only_peers_approved()
    {
        // Arrange
        var spec = Serializer().Deserialize<ChangeRequest>("""{"rule": {"spec": "change.author-is-approver"}}""");
        var request = Request(author: "alice", approvals: [new Approval("bob", Now, [])]);

        // Act
        var result = spec.Evaluate(request);

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Assertions.ShouldBe(["no self-approval"]);
    }

    // --- change.approver-has-role ---

    [Fact]
    public void Should_be_satisfied_when_an_approver_holds_the_role()
    {
        // Arrange
        var spec = Serializer().Deserialize<ChangeRequest>(
            """{"rule": {"spec": "change.approver-has-role", "args": {"role": "security"}}}""");
        var request = Request(approvals: [new Approval("bob", Now, ["security"])]);

        // Act
        var result = spec.Evaluate(request);

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["an approver holds role 'security'"]);
    }

    [Fact]
    public void Should_be_unsatisfied_when_no_approver_holds_the_role()
    {
        // Arrange
        var spec = Serializer().Deserialize<ChangeRequest>(
            """{"rule": {"spec": "change.approver-has-role", "args": {"role": "security"}}}""");
        var request = Request(approvals: [new Approval("bob", Now, ["reviewer"])]);

        // Act
        var result = spec.Evaluate(request);

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Assertions.ShouldBe(["no approver holds role 'security'"]);
    }

    // --- change.is-rollback ---

    [Fact]
    public void Should_be_satisfied_when_a_change_is_a_rollback()
    {
        // Arrange
        var spec = Serializer().Deserialize<ChangeRequest>("""{"rule": {"spec": "change.is-rollback"}}""");
        var request = Request(proposedChanges: [AProposedChange(isRollback: true)]);

        // Act
        var result = spec.Evaluate(request);

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["change is a rollback"]);
    }

    [Fact]
    public void Should_be_unsatisfied_when_no_change_is_a_rollback()
    {
        // Arrange
        var spec = Serializer().Deserialize<ChangeRequest>("""{"rule": {"spec": "change.is-rollback"}}""");
        var request = Request(proposedChanges: [AProposedChange(isRollback: false)]);

        // Act
        var result = spec.Evaluate(request);

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Assertions.ShouldBe(["change is not a rollback"]);
    }

    // --- change.is-creation ---

    [Fact]
    public void Should_be_satisfied_when_a_change_creates_an_artefact()
    {
        // Arrange
        var spec = Serializer().Deserialize<ChangeRequest>("""{"rule": {"spec": "change.is-creation"}}""");
        var request = Request(proposedChanges: [AProposedChange(isCreation: true)]);

        // Act
        var result = spec.Evaluate(request);

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["change creates an artefact"]);
    }

    [Fact]
    public void Should_be_unsatisfied_when_no_change_creates_an_artefact()
    {
        // Arrange
        var spec = Serializer().Deserialize<ChangeRequest>("""{"rule": {"spec": "change.is-creation"}}""");
        var request = Request(proposedChanges: [AProposedChange(isCreation: false)]);

        // Act
        var result = spec.Evaluate(request);

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Assertions.ShouldBe(["change creates nothing"]);
    }

    // --- change.is-deletion ---

    [Fact]
    public void Should_be_satisfied_when_a_change_deletes_an_artefact()
    {
        // Arrange
        var spec = Serializer().Deserialize<ChangeRequest>("""{"rule": {"spec": "change.is-deletion"}}""");
        var request = Request(proposedChanges: [AProposedChange(isDeletion: true)]);

        // Act
        var result = spec.Evaluate(request);

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["change deletes an artefact"]);
    }

    [Fact]
    public void Should_be_unsatisfied_when_no_change_deletes_an_artefact()
    {
        // Arrange
        var spec = Serializer().Deserialize<ChangeRequest>("""{"rule": {"spec": "change.is-deletion"}}""");
        var request = Request(proposedChanges: [AProposedChange(isDeletion: false)]);

        // Act
        var result = spec.Evaluate(request);

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Assertions.ShouldBe(["change deletes nothing"]);
    }

    // --- change.is-metadata-only ---

    [Fact]
    public void Should_be_satisfied_when_every_change_is_metadata_only()
    {
        // Arrange
        var spec = Serializer().Deserialize<ChangeRequest>("""{"rule": {"spec": "change.is-metadata-only"}}""");
        var request = Request(proposedChanges:
        [
            AProposedChange(name: "a", isMetadataOnly: true),
            AProposedChange(name: "b", isMetadataOnly: true)
        ]);

        // Act
        var result = spec.Evaluate(request);

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["change is metadata-only"]);
    }

    [Fact]
    public void Should_be_unsatisfied_when_any_change_alters_logic()
    {
        // Arrange
        var spec = Serializer().Deserialize<ChangeRequest>("""{"rule": {"spec": "change.is-metadata-only"}}""");
        var request = Request(proposedChanges:
        [
            AProposedChange(name: "a", isMetadataOnly: true),
            AProposedChange(name: "b", isMetadataOnly: false)
        ]);

        // Act
        var result = spec.Evaluate(request);

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Assertions.ShouldBe(["change alters logic"]);
    }

    // --- change.touches-async-spec ---

    [Fact]
    public void Should_be_satisfied_when_a_change_touches_an_async_spec()
    {
        // Arrange
        var spec = Serializer().Deserialize<ChangeRequest>("""{"rule": {"spec": "change.touches-async-spec"}}""");
        var request = Request(proposedChanges: [AProposedChange(touchesAsyncSpec: true)]);

        // Act
        var result = spec.Evaluate(request);

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["change touches an async spec"]);
    }

    [Fact]
    public void Should_be_unsatisfied_when_no_change_touches_an_async_spec()
    {
        // Arrange
        var spec = Serializer().Deserialize<ChangeRequest>("""{"rule": {"spec": "change.touches-async-spec"}}""");
        var request = Request(proposedChanges: [AProposedChange(touchesAsyncSpec: false)]);

        // Act
        var result = spec.Evaluate(request);

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Assertions.ShouldBe(["change touches no async spec"]);
    }

    // --- Maker-checker composition ---

    [Fact]
    public void Should_express_maker_checker_as_a_composition()
    {
        // Arrange — maker-checker = approver-count-at-least(1) & !author-is-approver (ticket 12:
        // segregation of duties is a workflow property, not a grant)
        var serializer = new RuleSerializer(GateSpecs.CreateRegistry());
        var gate = serializer.Deserialize<ChangeRequest>(
            """
            {"rule": {"and": [
                {"spec": "change.approver-count-at-least", "args": {"n": 1}},
                {"not": {"spec": "change.author-is-approver"}}
            ]}}
            """);
        var selfApproved = Request(author: "alice", approvals: [new Approval("alice", Now, [])]);
        var peerApproved = Request(author: "alice", approvals: [new Approval("bob", Now, [])]);

        // Act & Assert
        gate.Evaluate(selfApproved).Satisfied.ShouldBeFalse();
        gate.Evaluate(peerApproved).Satisfied.ShouldBeTrue();
    }
}
