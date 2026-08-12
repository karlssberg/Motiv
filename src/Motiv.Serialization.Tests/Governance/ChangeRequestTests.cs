namespace Motiv.Serialization.Tests;

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
        var target = new ChangeTarget(ChangeTargetKind.Proposition, "pricing.eu.vat");

        target.Namespace.ShouldBe("pricing.eu");
    }

    [Fact]
    public void Should_derive_an_empty_namespace_for_a_bare_name()
    {
        var target = new ChangeTarget(ChangeTargetKind.Proposition, "vat");

        target.Namespace.ShouldBe(string.Empty);
    }

    // --- Construction ---

    [Fact]
    public void Should_start_as_a_draft_with_no_approvals()
    {
        var request = ADraftChangeRequest();

        request.Status.ShouldBe(ChangeRequestStatus.Draft);
        request.Approvals.ShouldBeEmpty();
        request.RejectionReason.ShouldBeNull();
        request.PublishedUnderBreakGlass.ShouldBeFalse();
    }

    [Fact]
    public void Should_expose_the_constructor_arguments()
    {
        var id = Guid.NewGuid();
        var proposedChanges = new[] { AProposedChange() };

        var request = new ChangeRequest(id, "alice", "adjust VAT rate", proposedChanges);

        request.Id.ShouldBe(id);
        request.Author.ShouldBe("alice");
        request.ChangeNote.ShouldBe("adjust VAT rate");
        request.ProposedChanges.ShouldBe(proposedChanges);
    }

    [Fact]
    public void Should_reject_construction_with_no_proposed_changes()
    {
        Should.Throw<ArgumentException>(() =>
            new ChangeRequest(Guid.NewGuid(), "alice", "adjust VAT rate", []));
    }

    // --- AddApproval ---

    [Fact]
    public void Should_move_from_draft_to_in_review_on_first_approval()
    {
        var request = ADraftChangeRequest();

        request.AddApproval(AnApproval());

        request.Status.ShouldBe(ChangeRequestStatus.InReview);
        request.Approvals.Count.ShouldBe(1);
    }

    [Fact]
    public void Should_accumulate_approvals_while_in_review()
    {
        var request = ADraftChangeRequest();

        request.AddApproval(AnApproval("bob"));
        request.AddApproval(AnApproval("carol"));

        request.Status.ShouldBe(ChangeRequestStatus.InReview);
        request.Approvals.Count.ShouldBe(2);
        request.Approvals.Select(a => a.Approver).ShouldBe(["bob", "carol"]);
    }

    [Fact]
    public void Should_reject_approval_from_a_rejected_request()
    {
        var request = ADraftChangeRequest();
        request.MarkRejected("not needed");

        Should.Throw<InvalidOperationException>(() => request.AddApproval(AnApproval()));
    }

    [Fact]
    public void Should_reject_approval_from_a_withdrawn_request()
    {
        var request = ADraftChangeRequest();
        request.MarkWithdrawn();

        Should.Throw<InvalidOperationException>(() => request.AddApproval(AnApproval()));
    }

    [Fact]
    public void Should_reject_approval_from_a_published_request()
    {
        var request = ADraftChangeRequest();
        request.MarkPublished(underBreakGlass: false);

        Should.Throw<InvalidOperationException>(() => request.AddApproval(AnApproval()));
    }

    // --- MarkPublished ---

    [Fact]
    public void Should_publish_from_draft()
    {
        var request = ADraftChangeRequest();

        request.MarkPublished(underBreakGlass: false);

        request.Status.ShouldBe(ChangeRequestStatus.Published);
        request.PublishedUnderBreakGlass.ShouldBeFalse();
    }

    [Fact]
    public void Should_publish_from_in_review()
    {
        var request = ADraftChangeRequest();
        request.AddApproval(AnApproval());

        request.MarkPublished(underBreakGlass: false);

        request.Status.ShouldBe(ChangeRequestStatus.Published);
    }

    [Fact]
    public void Should_stamp_break_glass_publication()
    {
        var request = ADraftChangeRequest();

        request.MarkPublished(underBreakGlass: true);

        request.Status.ShouldBe(ChangeRequestStatus.Published);
        request.PublishedUnderBreakGlass.ShouldBeTrue();
    }

    [Fact]
    public void Should_reject_publishing_an_already_published_request()
    {
        var request = ADraftChangeRequest();
        request.MarkPublished(underBreakGlass: false);

        Should.Throw<InvalidOperationException>(() => request.MarkPublished(underBreakGlass: false));
    }

    [Fact]
    public void Should_reject_publishing_a_rejected_request()
    {
        var request = ADraftChangeRequest();
        request.MarkRejected("not needed");

        Should.Throw<InvalidOperationException>(() => request.MarkPublished(underBreakGlass: false));
    }

    [Fact]
    public void Should_reject_publishing_a_withdrawn_request()
    {
        var request = ADraftChangeRequest();
        request.MarkWithdrawn();

        Should.Throw<InvalidOperationException>(() => request.MarkPublished(underBreakGlass: false));
    }

    // --- MarkRejected ---

    [Fact]
    public void Should_reject_from_draft_and_store_the_reason()
    {
        var request = ADraftChangeRequest();

        request.MarkRejected("out of scope");

        request.Status.ShouldBe(ChangeRequestStatus.Rejected);
        request.RejectionReason!.ShouldBe("out of scope");
    }

    [Fact]
    public void Should_reject_from_in_review()
    {
        var request = ADraftChangeRequest();
        request.AddApproval(AnApproval());

        request.MarkRejected("out of scope");

        request.Status.ShouldBe(ChangeRequestStatus.Rejected);
    }

    [Fact]
    public void Should_reject_rejecting_an_already_rejected_request()
    {
        var request = ADraftChangeRequest();
        request.MarkRejected("out of scope");

        Should.Throw<InvalidOperationException>(() => request.MarkRejected("again"));
    }

    [Fact]
    public void Should_reject_rejecting_a_published_request()
    {
        var request = ADraftChangeRequest();
        request.MarkPublished(underBreakGlass: false);

        Should.Throw<InvalidOperationException>(() => request.MarkRejected("too late"));
    }

    [Fact]
    public void Should_reject_rejecting_a_withdrawn_request()
    {
        var request = ADraftChangeRequest();
        request.MarkWithdrawn();

        Should.Throw<InvalidOperationException>(() => request.MarkRejected("too late"));
    }

    // --- MarkWithdrawn ---

    [Fact]
    public void Should_withdraw_from_draft()
    {
        var request = ADraftChangeRequest();

        request.MarkWithdrawn();

        request.Status.ShouldBe(ChangeRequestStatus.Withdrawn);
    }

    [Fact]
    public void Should_withdraw_from_in_review()
    {
        var request = ADraftChangeRequest();
        request.AddApproval(AnApproval());

        request.MarkWithdrawn();

        request.Status.ShouldBe(ChangeRequestStatus.Withdrawn);
    }

    [Fact]
    public void Should_reject_withdrawing_an_already_withdrawn_request()
    {
        var request = ADraftChangeRequest();
        request.MarkWithdrawn();

        Should.Throw<InvalidOperationException>(() => request.MarkWithdrawn());
    }

    [Fact]
    public void Should_reject_withdrawing_a_rejected_request()
    {
        var request = ADraftChangeRequest();
        request.MarkRejected("out of scope");

        Should.Throw<InvalidOperationException>(() => request.MarkWithdrawn());
    }

    [Fact]
    public void Should_reject_withdrawing_a_published_request()
    {
        var request = ADraftChangeRequest();
        request.MarkPublished(underBreakGlass: false);

        Should.Throw<InvalidOperationException>(() => request.MarkWithdrawn());
    }
}
