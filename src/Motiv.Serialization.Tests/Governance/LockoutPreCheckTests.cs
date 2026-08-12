namespace Motiv.Serialization.Tests.Governance;

/// <summary>
/// The lockout pre-check: before <see cref="ApprovalGate.SetGate"/> persists a candidate gate
/// document, it asks the document to judge <see cref="SyntheticChangeRequests.MaximallyApprovable"/>
/// — the friendliest change imaginable. A document that would refuse even that is refused itself,
/// rather than locking out every future change.
/// </summary>
public class LockoutPreCheckTests
{
    private const string MakerCheckerDocument =
        """
        {"rule": {"and": [
            {"spec": "change.approver-count-at-least", "args": {"n": 1}},
            {"not": {"spec": "change.author-is-approver"}}
        ]}}
        """;

    private const string GhostRoleDocument =
        """{"rule": {"spec": "change.approver-has-role", "args": {"role": "ghost"}}}""";

    // -- SyntheticChangeRequests.MaximallyApprovable builder shape --

    [Fact]
    public void Should_build_a_request_with_100_distinct_approvers()
    {
        // Act
        var request = SyntheticChangeRequests.MaximallyApprovable(["motiv-dev"]);

        // Assert
        request.Approvals.Count.ShouldBe(100);
        request.Approvals.Select(a => a.Approver).Distinct().Count().ShouldBe(100);
        request.Approvals.Select(a => a.Approver).ShouldContain("synthetic-approver-1");
        request.Approvals.Select(a => a.Approver).ShouldContain("synthetic-approver-100");
    }

    [Fact]
    public void Should_have_every_approval_carry_every_known_role()
    {
        // Arrange
        IReadOnlyCollection<string> knownRoles = ["motiv-dev", "auditor"];

        // Act
        var request = SyntheticChangeRequests.MaximallyApprovable(knownRoles);

        // Assert
        foreach (var approval in request.Approvals)
            approval.Roles.ShouldBe(knownRoles, ignoreOrder: true);
    }

    [Fact]
    public void Should_use_a_deterministic_timestamp_for_every_approval()
    {
        // Act
        var request = SyntheticChangeRequests.MaximallyApprovable(["motiv-dev"]);

        // Assert
        request.Approvals.Select(a => a.TimestampUtc).Distinct().ShouldBe([new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)]);
    }

    [Fact]
    public void Should_author_the_request_as_someone_not_among_the_approvers()
    {
        // Act
        var request = SyntheticChangeRequests.MaximallyApprovable(["motiv-dev"]);

        // Assert
        request.Author.ShouldBe("synthetic-author");
        request.Approvals.Select(a => a.Approver).ShouldNotContain(request.Author);
    }

    [Fact]
    public void Should_target_a_single_rule_change_under_motiv_governance()
    {
        // Act
        var request = SyntheticChangeRequests.MaximallyApprovable(["motiv-dev"]);

        // Assert
        request.ProposedChanges.Count.ShouldBe(1);
        var change = request.ProposedChanges[0];
        change.Target.Kind.ShouldBe(ChangeTargetKind.Rule);
        change.Target.Name.ShouldBe("motiv.governance.gate");
        change.ProposedDocumentJson.ShouldNotBeNull();
        change.BaseVersion.ShouldBe(1);
    }

    [Fact]
    public void Should_classify_the_proposed_change_as_all_false()
    {
        // Act
        var request = SyntheticChangeRequests.MaximallyApprovable(["motiv-dev"]);

        // Assert
        var classification = request.ProposedChanges[0].Classification;
        classification.IsCreation.ShouldBeFalse();
        classification.IsDeletion.ShouldBeFalse();
        classification.IsMetadataOnly.ShouldBeFalse();
        classification.TouchesAsyncSpec.ShouldBeFalse();
        classification.IsRollback.ShouldBeFalse();
        classification.RollbackOfVersion.ShouldBeNull();
    }

    // -- ApprovalGate.SetGate pre-check behaviour --

    [Fact]
    public void Should_refuse_a_gate_document_that_the_synthetic_request_cannot_satisfy()
    {
        // Arrange — a gate requiring approval from a role no one in the synthetic request will have
        var gate = new ApprovalGate();

        // Act
        var updateResult = gate.SetGate(GhostRoleDocument, ["motiv-dev"]);

        // Assert — refused as a would-be lockout, not persisted, and the refusal names why
        updateResult.Outcome.ShouldBe(GateUpdateOutcome.WouldLockOut);
        updateResult.PreCheck.ShouldNotBeNull();
        updateResult.PreCheck.MayPublish.ShouldBeFalse();
        updateResult.PreCheck.Assertions.ShouldContain("no approver holds role 'ghost'");
        gate.DocumentJson.ShouldBeNull();
    }

    [Fact]
    public void Should_leave_the_gate_evaluating_permissively_after_a_would_lock_out_refusal()
    {
        // Arrange
        var gate = new ApprovalGate();

        // Act
        gate.SetGate(GhostRoleDocument, ["motiv-dev"]);
        var decision = gate.Evaluate(
            new ChangeRequest(
                Guid.NewGuid(), "alice", "a change",
                [
                    new ProposedChange(
                        new ChangeTarget(ChangeTargetKind.Proposition, "pricing.eu.vat"),
                        "{}",
                        BaseVersion: 1,
                        new ChangeClassification(false, false, false, false, false, null))
                ]));

        // Assert — the gate is exactly as it was before the refused attempt: permissive default
        decision.MayPublish.ShouldBeTrue();
        decision.Reason.ShouldBe(ApprovalGate.NoGateConfiguredReason);
    }

    [Fact]
    public void Should_accept_a_gate_document_the_synthetic_request_satisfies()
    {
        // Arrange — an ordinary maker-checker gate: 100 non-self approvers easily clears "at least 1"
        var gate = new ApprovalGate();

        // Act
        var updateResult = gate.SetGate(MakerCheckerDocument, ["motiv-dev"]);

        // Assert
        updateResult.Outcome.ShouldBe(GateUpdateOutcome.Updated);
        updateResult.PreCheck.ShouldBeNull();
        gate.DocumentJson!.ShouldBe(MakerCheckerDocument);
    }

    [Fact]
    public void Should_accept_a_gate_document_the_synthetic_request_satisfies_with_no_known_roles()
    {
        // Arrange — maker-checker never inspects roles, so an empty known-roles set still passes
        var gate = new ApprovalGate();

        // Act
        var updateResult = gate.SetGate(MakerCheckerDocument, []);

        // Assert
        updateResult.Outcome.ShouldBe(GateUpdateOutcome.Updated);
        gate.DocumentJson!.ShouldBe(MakerCheckerDocument);
    }

    [Fact]
    public void Should_never_pre_check_a_reset_to_permissive()
    {
        // Arrange — a gate already configured with a maker-checker document
        var gate = new ApprovalGate();
        gate.SetGate(MakerCheckerDocument, ["motiv-dev"]);

        // Act — reset to permissive with no known roles at all; the recovery path must always work
        var updateResult = gate.SetGate(null, []);

        // Assert
        updateResult.Outcome.ShouldBe(GateUpdateOutcome.Updated);
        gate.DocumentJson.ShouldBeNull();
    }
}
