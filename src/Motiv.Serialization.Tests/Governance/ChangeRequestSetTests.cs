namespace Motiv.Serialization.Tests.Governance;

public class ChangeRequestSetTests
{
    private const string MakerCheckerGate =
        """
        {"rule": {"and": [
            {"spec": "change.approver-count-at-least", "args": {"n": 1}},
            {"not": {"spec": "change.author-is-approver"}}
        ]}}
        """;

    private const string EligibleIsAdult = """{ "rule": { "spec": "customer.is-adult" } }""";
    private const string CheckoutUsesEligible = """{ "rule": { "spec": "customer.eligible" } }""";

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static SpecBase<Customer, string> IsAdult { get; } =
        Spec.Build((Customer c) => c.Age >= 18).WhenTrue("adult").WhenFalse("minor").Create();

    private sealed class CanCheckoutRule() : Rule<Customer, string>("can-checkout", IsActive);

    private sealed record Host(
        ChangeRequestSet Changes,
        ApprovalGate Gate,
        RuleSet Rules,
        PropositionSet Propositions,
        CanCheckoutRule Rule);

    private static Host NewHost()
    {
        var registry = new SpecRegistry()
            .Register("customer.is-active", IsActive)
            .Register("customer.is-adult", IsAdult);
        var scope = new BindingScope(registry);
        var propositions = new PropositionSet(scope, new InMemoryPropositionStore()).AddModel<Customer>("customer");
        var rule = new CanCheckoutRule();
        var rules = new RuleSet(scope).Add(rule);
        var gate = new ApprovalGate();
        return new Host(new ChangeRequestSet(gate, rules, propositions), gate, rules, propositions, rule);
    }

    /// <summary>
    /// The coordinated change the envelope exists for: a brand-new proposition and a rule edit that
    /// references it. Neither half is publishable on its own — the rule cannot bind until the
    /// proposition is live — so the pair is the smallest change that proves the envelope is real.
    /// </summary>
    private static IReadOnlyList<NewProposedChange> CoordinatedPair(int ruleBaseVersion = 1) =>
    [
        new(ChangeTargetKind.Proposition, "customer.eligible", EligibleIsAdult,
            BaseVersion: 0, RollbackOfVersion: null, ModelTypeId: "customer"),
        new(ChangeTargetKind.Rule, "can-checkout", CheckoutUsesEligible,
            BaseVersion: ruleBaseVersion, RollbackOfVersion: null)
    ];

    [Fact]
    public void Should_publish_a_two_change_envelope_atomically()
    {
        // Arrange
        var host = NewHost();
        var created = host.Changes.Create("alice", "route checkout through eligibility", CoordinatedPair());
        var inactiveAdult = new Customer(IsActive: false, Age: 30);
        host.Rule.Evaluate(inactiveAdult).Satisfied.ShouldBeFalse();

        // Act
        var published = host.Changes.Publish(created.Change!.Id, breakGlassActive: false);

        // Assert
        published.Outcome.ShouldBe(ChangeRequestOutcome.Ok);
        published.Change!.Status.ShouldBe(ChangeRequestStatus.Published);
        published.PublishedVersions!["customer.eligible"].ShouldBe(1);
        published.PublishedVersions!["can-checkout"].ShouldBe(2);
        host.Propositions.DocumentJsonOf("customer.eligible")!.ShouldBe(EligibleIsAdult);
        host.Rule.Evaluate(inactiveAdult).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_classify_a_new_proposition_as_a_creation_at_create_time()
    {
        // Arrange
        var host = NewHost();

        // Act
        var created = host.Changes.Create("alice", "a note", CoordinatedPair());

        // Assert
        created.Outcome.ShouldBe(ChangeRequestOutcome.Ok);
        created.Change!.ProposedChanges[0].Classification.IsCreation.ShouldBeTrue();
        created.Change!.ProposedChanges[1].Classification.IsCreation.ShouldBeFalse();
    }

    /// <summary>
    /// The atomicity test. The rule half carries a stale base version, so the whole envelope is
    /// refused — including the proposition half, which on its own would have published cleanly.
    /// </summary>
    [Fact]
    public void Should_publish_neither_change_when_one_base_version_is_stale()
    {
        // Arrange
        var host = NewHost();
        var created = host.Changes.Create("alice", "a note", CoordinatedPair(ruleBaseVersion: 7));

        // Act
        var published = host.Changes.Publish(created.Change!.Id, breakGlassActive: false);

        // Assert
        published.Outcome.ShouldBe(ChangeRequestOutcome.VersionConflict);
        published.FailedTarget.ShouldBe(new ChangeTarget(ChangeTargetKind.Rule, "can-checkout"));
        published.ConflictVersion.ShouldBe(1);

        // Neither half moved
        host.Propositions.DocumentJsonOf("customer.eligible").ShouldBeNull();
        host.Rules.FindEntry("can-checkout")!.Version.ShouldBe(1);
        host.Changes.Find(created.Change!.Id)!.Status.ShouldBe(ChangeRequestStatus.Draft);
    }

    [Fact]
    public void Should_publish_neither_change_when_the_proposition_half_is_stale()
    {
        // Arrange — the proposition already exists, so a base version of 0 is stale
        var host = NewHost();
        host.Propositions.Create("customer.eligible", "customer", EligibleIsAdult, null)
            .Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        var created = host.Changes.Create("alice", "a note", CoordinatedPair());

        // Act
        var published = host.Changes.Publish(created.Change!.Id, breakGlassActive: false);

        // Assert
        published.Outcome.ShouldBe(ChangeRequestOutcome.VersionConflict);
        published.FailedTarget.ShouldBe(new ChangeTarget(ChangeTargetKind.Proposition, "customer.eligible"));
        host.Rules.FindEntry("can-checkout")!.Version.ShouldBe(1);
    }

    [Fact]
    public void Should_block_publication_when_the_gate_is_unsatisfied()
    {
        // Arrange
        var host = NewHost();
        host.Gate.SetGate(MakerCheckerGate, []).Outcome.ShouldBe(GateUpdateOutcome.Updated);
        var created = host.Changes.Create("alice", "a note", CoordinatedPair());

        // Act
        var published = host.Changes.Publish(created.Change!.Id, breakGlassActive: false);

        // Assert
        published.Outcome.ShouldBe(ChangeRequestOutcome.GateBlocked);
        published.Gate!.MayPublish.ShouldBeFalse();
        published.Gate!.Assertions.ShouldContain("change has fewer than 1 approvals");

        // No state change
        host.Propositions.DocumentJsonOf("customer.eligible").ShouldBeNull();
        host.Rules.FindEntry("can-checkout")!.Version.ShouldBe(1);
        host.Changes.Find(created.Change!.Id)!.Status.ShouldBe(ChangeRequestStatus.Draft);
    }

    [Fact]
    public void Should_publish_once_a_peer_approval_satisfies_the_gate()
    {
        // Arrange
        var host = NewHost();
        host.Gate.SetGate(MakerCheckerGate, []);
        var created = host.Changes.Create("alice", "a note", CoordinatedPair());

        // Act — the request is one mutable object, so its post-approval status is captured here
        var approved = host.Changes.Approve(created.Change!.Id, "bob", ["reviewer"]);
        var statusAfterApproval = approved.Change!.Status;
        var published = host.Changes.Publish(created.Change!.Id, breakGlassActive: false);

        // Assert
        approved.Outcome.ShouldBe(ChangeRequestOutcome.Ok);
        statusAfterApproval.ShouldBe(ChangeRequestStatus.InReview);
        approved.Change!.Approvals.Single().Approver.ShouldBe("bob");
        approved.Change!.Approvals.Single().Roles.ShouldBe(["reviewer"]);
        published.Outcome.ShouldBe(ChangeRequestOutcome.Ok);
        published.Change!.PublishedUnderBreakGlass.ShouldBeFalse();
        host.Rule.Evaluate(new Customer(IsActive: false, Age: 30)).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_bypass_a_blocking_gate_under_break_glass_and_stamp_the_request()
    {
        // Arrange
        var host = NewHost();
        host.Gate.SetGate(MakerCheckerGate, []);
        var created = host.Changes.Create("alice", "production is down", CoordinatedPair());

        // Act
        var published = host.Changes.Publish(created.Change!.Id, breakGlassActive: true);

        // Assert
        published.Outcome.ShouldBe(ChangeRequestOutcome.Ok);
        published.Change!.Status.ShouldBe(ChangeRequestStatus.Published);
        published.Change!.PublishedUnderBreakGlass.ShouldBeTrue();
        host.Rule.Evaluate(new Customer(IsActive: false, Age: 30)).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_refuse_a_withdrawal_by_someone_other_than_the_author()
    {
        // Arrange
        var host = NewHost();
        var created = host.Changes.Create("alice", "a note", CoordinatedPair());

        // Act
        var withdrawn = host.Changes.Withdraw(created.Change!.Id, "mallory");

        // Assert
        withdrawn.Outcome.ShouldBe(ChangeRequestOutcome.InvalidState);
        host.Changes.Find(created.Change!.Id)!.Status.ShouldBe(ChangeRequestStatus.Draft);
    }

    [Fact]
    public void Should_allow_the_author_to_withdraw_their_own_request()
    {
        // Arrange
        var host = NewHost();
        var created = host.Changes.Create("alice", "a note", CoordinatedPair());

        // Act
        var withdrawn = host.Changes.Withdraw(created.Change!.Id, "alice");

        // Assert
        withdrawn.Outcome.ShouldBe(ChangeRequestOutcome.Ok);
        withdrawn.Change!.Status.ShouldBe(ChangeRequestStatus.Withdrawn);
    }

    [Fact]
    public void Should_refuse_to_publish_a_rejected_change_request()
    {
        // Arrange
        var host = NewHost();
        var created = host.Changes.Create("alice", "a note", CoordinatedPair());
        host.Changes.Reject(created.Change!.Id, "not now").Outcome.ShouldBe(ChangeRequestOutcome.Ok);

        // Act
        var published = host.Changes.Publish(created.Change!.Id, breakGlassActive: false);

        // Assert
        published.Outcome.ShouldBe(ChangeRequestOutcome.InvalidState);
        published.Change!.Status.ShouldBe(ChangeRequestStatus.Rejected);
        published.Change!.RejectionReason!.ShouldBe("not now");
        host.Rules.FindEntry("can-checkout")!.Version.ShouldBe(1);
    }

    [Fact]
    public void Should_report_an_unknown_change_request_as_not_found()
    {
        // Arrange
        var host = NewHost();

        // Act
        var published = host.Changes.Publish(Guid.NewGuid(), breakGlassActive: false);

        // Assert
        published.Outcome.ShouldBe(ChangeRequestOutcome.NotFound);
        published.Change.ShouldBeNull();
    }

    [Fact]
    public void Should_refuse_a_rule_set_and_proposition_set_that_do_not_share_a_binding_scope()
    {
        // Arrange — two independent scopes, so a publish could not be atomic across them
        var propositions = new PropositionSet(
            new BindingScope(new SpecRegistry()), new InMemoryPropositionStore());
        var rules = new RuleSet(new BindingScope(new SpecRegistry()));

        // Act / Assert
        Should.Throw<InvalidOperationException>(
            () => new ChangeRequestSet(new ApprovalGate(), rules, propositions));
    }

    [Fact]
    public void Should_report_an_unbindable_document_as_invalid_without_touching_anything()
    {
        // Arrange — the rule half references a proposition nothing in the envelope creates
        var host = NewHost();
        var created = host.Changes.Create("alice", "a note",
        [
            new(ChangeTargetKind.Rule, "can-checkout", """{ "rule": { "spec": "customer.missing" } }""",
                BaseVersion: 1, RollbackOfVersion: null)
        ]);

        // Act
        var published = host.Changes.Publish(created.Change!.Id, breakGlassActive: false);

        // Assert
        published.Outcome.ShouldBe(ChangeRequestOutcome.Invalid);
        published.FailedTarget.ShouldBe(new ChangeTarget(ChangeTargetKind.Rule, "can-checkout"));
        published.Errors.ShouldNotBeEmpty();
        host.Rules.FindEntry("can-checkout")!.Version.ShouldBe(1);
    }

    private sealed record Customer(bool IsActive, int Age);
}
