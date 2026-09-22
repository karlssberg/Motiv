namespace Motiv.Serialization.Tests.Governance;

public class ChangeRequestTests
{
    private static ProposedChange AProposedChange(string name = "pricing.eu.vat") =>
        new(
            new ChangeTarget(ChangeTargetKind.Proposition, name),
            "{}",
            BaseVersion: 1,
            new ChangeClassification(
                IsCreation: false,
                IsDeletion: false,
                IsMetadataOnly: false,
                TouchesAsyncSpec: false,
                IsRollback: false,
                RollbackOfVersion: null));

    private static ChangeRequest ADraftChangeRequest() =>
        new(Guid.NewGuid(), "alice", "adjust VAT rate", [AProposedChange()]);

    private static Approval AnApproval(string approver = "bob") =>
        new(approver, DateTimeOffset.UtcNow, ["reviewer"]);

    // --- ChangeTarget.Namespace ---

    [Fact]
    public void Should_derive_namespace_as_substring_before_the_last_dot()
    {
        // Arrange
        var target = new ChangeTarget(ChangeTargetKind.Proposition, "pricing.eu.vat");

        // Act
        var @namespace = target.Namespace;

        // Assert
        @namespace.ShouldBe("pricing.eu");
    }

    [Fact]
    public void Should_derive_an_empty_namespace_for_a_bare_name()
    {
        // Arrange
        var target = new ChangeTarget(ChangeTargetKind.Proposition, "vat");

        // Act
        var @namespace = target.Namespace;

        // Assert
        @namespace.ShouldBe(string.Empty);
    }

    [Fact]
    public void Should_recompute_namespace_after_a_with_expression_changes_the_name()
    {
        // Arrange
        var target = new ChangeTarget(ChangeTargetKind.Proposition, "pricing.eu.vat");

        // Act
        var renamed = target with { Name = "x.y" };

        // Assert
        renamed.Namespace.ShouldBe("x");
    }

    // --- Construction ---

    [Fact]
    public void Should_start_as_a_draft_with_no_approvals()
    {
        // Arrange
        var request = ADraftChangeRequest();

        // Act & Assert
        request.Status.ShouldBe(ChangeRequestStatus.Draft);
        request.Approvals.ShouldBeEmpty();
        request.RejectionReason.ShouldBeNull();
        request.PublishedUnderBreakGlass.ShouldBeFalse();
    }

    [Fact]
    public void Should_expose_the_constructor_arguments()
    {
        // Arrange
        var id = Guid.NewGuid();
        var proposedChanges = new[] { AProposedChange() };

        // Act
        var request = new ChangeRequest(id, "alice", "adjust VAT rate", proposedChanges);

        // Assert
        request.Id.ShouldBe(id);
        request.Author.ShouldBe("alice");
        request.ChangeNote.ShouldBe("adjust VAT rate");
        request.ProposedChanges.ShouldBe(proposedChanges);
    }

    [Fact]
    public void Should_reject_construction_with_no_proposed_changes()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            new ChangeRequest(Guid.NewGuid(), "alice", "adjust VAT rate", []));
    }

    // --- AddApproval ---

    [Fact]
    public void Should_move_from_draft_to_in_review_on_first_approval()
    {
        // Arrange
        var request = ADraftChangeRequest();

        // Act
        request.AddApproval(AnApproval());

        // Assert
        request.Status.ShouldBe(ChangeRequestStatus.InReview);
        request.Approvals.Count.ShouldBe(1);
    }

    [Fact]
    public void Should_accumulate_approvals_while_in_review()
    {
        // Arrange
        var request = ADraftChangeRequest();

        // Act
        request.AddApproval(AnApproval("bob"));
        request.AddApproval(AnApproval("carol"));

        // Assert
        request.Status.ShouldBe(ChangeRequestStatus.InReview);
        request.Approvals.Count.ShouldBe(2);
        request.Approvals.Select(a => a.Approver).ShouldBe(["bob", "carol"]);
    }

    [Fact]
    public void Should_not_expose_the_mutable_backing_list_via_approvals()
    {
        // Arrange
        var request = ADraftChangeRequest();
        request.AddApproval(AnApproval());

        // Act
        var approvals = request.Approvals;

        // Assert
        (approvals as List<Approval>).ShouldBeNull();
    }

    [Fact]
    public void Should_reject_approval_from_a_rejected_request()
    {
        // Arrange
        var request = ADraftChangeRequest();
        request.MarkRejected("not needed");

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => request.AddApproval(AnApproval()));
    }

    [Fact]
    public void Should_reject_approval_from_a_withdrawn_request()
    {
        // Arrange
        var request = ADraftChangeRequest();
        request.MarkWithdrawn();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => request.AddApproval(AnApproval()));
    }

    [Fact]
    public void Should_reject_approval_from_a_published_request()
    {
        // Arrange
        var request = ADraftChangeRequest();
        request.MarkPublished(underBreakGlass: false);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => request.AddApproval(AnApproval()));
    }

    // --- MarkPublished ---

    [Fact]
    public void Should_publish_from_draft()
    {
        // Arrange
        var request = ADraftChangeRequest();

        // Act
        request.MarkPublished(underBreakGlass: false);

        // Assert
        request.Status.ShouldBe(ChangeRequestStatus.Published);
        request.PublishedUnderBreakGlass.ShouldBeFalse();
    }

    [Fact]
    public void Should_publish_from_in_review()
    {
        // Arrange
        var request = ADraftChangeRequest();
        request.AddApproval(AnApproval());

        // Act
        request.MarkPublished(underBreakGlass: false);

        // Assert
        request.Status.ShouldBe(ChangeRequestStatus.Published);
    }

    [Fact]
    public void Should_stamp_break_glass_publication()
    {
        // Arrange
        var request = ADraftChangeRequest();

        // Act
        request.MarkPublished(underBreakGlass: true);

        // Assert
        request.Status.ShouldBe(ChangeRequestStatus.Published);
        request.PublishedUnderBreakGlass.ShouldBeTrue();
    }

    [Fact]
    public void Should_reject_publishing_an_already_published_request()
    {
        // Arrange
        var request = ADraftChangeRequest();
        request.MarkPublished(underBreakGlass: false);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => request.MarkPublished(underBreakGlass: false));
    }

    [Fact]
    public void Should_reject_publishing_a_rejected_request()
    {
        // Arrange
        var request = ADraftChangeRequest();
        request.MarkRejected("not needed");

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => request.MarkPublished(underBreakGlass: false));
    }

    [Fact]
    public void Should_reject_publishing_a_withdrawn_request()
    {
        // Arrange
        var request = ADraftChangeRequest();
        request.MarkWithdrawn();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => request.MarkPublished(underBreakGlass: false));
    }

    // --- MarkRejected ---

    [Fact]
    public void Should_reject_from_draft_and_store_the_reason()
    {
        // Arrange
        var request = ADraftChangeRequest();

        // Act
        request.MarkRejected("out of scope");

        // Assert
        request.Status.ShouldBe(ChangeRequestStatus.Rejected);
        request.RejectionReason!.ShouldBe("out of scope");
    }

    [Fact]
    public void Should_reject_from_in_review()
    {
        // Arrange
        var request = ADraftChangeRequest();
        request.AddApproval(AnApproval());

        // Act
        request.MarkRejected("out of scope");

        // Assert
        request.Status.ShouldBe(ChangeRequestStatus.Rejected);
    }

    [Fact]
    public void Should_reject_rejecting_an_already_rejected_request()
    {
        // Arrange
        var request = ADraftChangeRequest();
        request.MarkRejected("out of scope");

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => request.MarkRejected("again"));
    }

    [Fact]
    public void Should_reject_rejecting_a_published_request()
    {
        // Arrange
        var request = ADraftChangeRequest();
        request.MarkPublished(underBreakGlass: false);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => request.MarkRejected("too late"));
    }

    [Fact]
    public void Should_reject_rejecting_a_withdrawn_request()
    {
        // Arrange
        var request = ADraftChangeRequest();
        request.MarkWithdrawn();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => request.MarkRejected("too late"));
    }

    // --- MarkWithdrawn ---

    [Fact]
    public void Should_withdraw_from_draft()
    {
        // Arrange
        var request = ADraftChangeRequest();

        // Act
        request.MarkWithdrawn();

        // Assert
        request.Status.ShouldBe(ChangeRequestStatus.Withdrawn);
    }

    [Fact]
    public void Should_withdraw_from_in_review()
    {
        // Arrange
        var request = ADraftChangeRequest();
        request.AddApproval(AnApproval());

        // Act
        request.MarkWithdrawn();

        // Assert
        request.Status.ShouldBe(ChangeRequestStatus.Withdrawn);
    }

    [Fact]
    public void Should_reject_withdrawing_an_already_withdrawn_request()
    {
        // Arrange
        var request = ADraftChangeRequest();
        request.MarkWithdrawn();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => request.MarkWithdrawn());
    }

    [Fact]
    public void Should_reject_withdrawing_a_rejected_request()
    {
        // Arrange
        var request = ADraftChangeRequest();
        request.MarkRejected("out of scope");

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => request.MarkWithdrawn());
    }

    [Fact]
    public void Should_reject_withdrawing_a_published_request()
    {
        // Arrange
        var request = ADraftChangeRequest();
        request.MarkPublished(underBreakGlass: false);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => request.MarkWithdrawn());
    }
}
