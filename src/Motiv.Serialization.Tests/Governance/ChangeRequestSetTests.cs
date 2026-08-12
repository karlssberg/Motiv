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

    private static PolicyBase<Customer, string> IsActivePolicy { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    // Composition returns a Spec, not a Policy, so this is what a PolicyRule must refuse to rebind to.
    private static SpecBase<Customer, string> ComposedNonPolicy { get; } = IsActive & IsAdult;

    private sealed class CanCheckoutRule() : Rule<Customer, string>("can-checkout", IsActive);

    private sealed class CanCheckoutPolicyRule() : PolicyRule<Customer, string>("can-checkout-policy", IsActivePolicy);

    /// <summary>A rule whose default is a *document*, so reverting re-acquires its references.</summary>
    private sealed class AuthoredDefaultRule()
        : Rule<Customer, string>("can-checkout-authored", RuleDocuments.FromJson(CheckoutUsesEligible));

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

    /// <summary>
    /// Two edits to one target validate against the same live version and then fight over it at
    /// apply — one lands, the other conflicts, leaving a live edit with no governance record. There
    /// is no defensible reading of the pair, so it is refused at authoring time.
    /// </summary>
    [Fact]
    public void Should_refuse_a_change_request_that_targets_one_artefact_twice()
    {
        // Arrange
        var host = NewHost();

        // Act
        var created = host.Changes.Create("alice", "a note",
        [
            new(ChangeTargetKind.Rule, "can-checkout", """{ "rule": { "spec": "customer.is-adult" } }""",
                BaseVersion: 1, RollbackOfVersion: null),
            new(ChangeTargetKind.Rule, "can-checkout", """{ "rule": { "spec": "customer.is-active" } }""",
                BaseVersion: 1, RollbackOfVersion: null)
        ]);

        // Assert
        created.Outcome.ShouldBe(ChangeRequestOutcome.Invalid);
        created.Change.ShouldBeNull();
        created.FailedTarget.ShouldBe(new ChangeTarget(ChangeTargetKind.Rule, "can-checkout"));
        host.Changes.All.ShouldBeEmpty();
        host.Rules.FindEntry("can-checkout")!.Version.ShouldBe(1);
    }

    /// <summary>
    /// The intermediate-state trap: validation walks the envelope in the same canonical order the
    /// apply does, so the creation of a proposition referencing one the same envelope withdraws is
    /// seen against a world where the withdrawal has *not* happened yet — exactly as the apply
    /// would see it — and the withdrawal is then refused while nothing has moved.
    /// </summary>
    [Fact]
    public void Should_refuse_an_envelope_that_creates_a_proposition_referencing_one_it_withdraws()
    {
        // Arrange
        var host = NewHost();
        host.Propositions.Create("customer.eligible", "customer", EligibleIsAdult, null)
            .Outcome.ShouldBe(PropositionUpdateOutcome.Created);

        var created = host.Changes.Create("alice", "a note",
        [
            new(ChangeTargetKind.Proposition, "customer.derived", CheckoutUsesEligible,
                BaseVersion: 0, RollbackOfVersion: null, ModelTypeId: "customer"),
            new(ChangeTargetKind.Proposition, "customer.eligible", null,
                BaseVersion: 1, RollbackOfVersion: null)
        ]);

        // Act
        var published = host.Changes.Publish(created.Change!.Id, breakGlassActive: false);

        // Assert
        published.Outcome.ShouldBe(ChangeRequestOutcome.Invalid);
        published.FailedTarget.ShouldBe(new ChangeTarget(ChangeTargetKind.Proposition, "customer.eligible"));
        published.Errors.ShouldContain(error => error.Message.Contains("customer.derived"));

        // Nothing applied
        host.Propositions.DocumentJsonOf("customer.derived").ShouldBeNull();
        host.Propositions.DocumentJsonOf("customer.eligible")!.ShouldBe(EligibleIsAdult);
        host.Changes.Find(created.Change!.Id)!.Status.ShouldBe(ChangeRequestStatus.Draft);
    }

    /// <summary>
    /// The referrer that blocks a withdrawal may be entirely outside the envelope. That is a pure
    /// live-graph query, so it belongs in the validation pass rather than being discovered by a
    /// core mid-apply.
    /// </summary>
    [Fact]
    public void Should_refuse_to_withdraw_a_proposition_a_live_rule_still_references()
    {
        // Arrange
        var host = NewHost();
        host.Propositions.Create("customer.eligible", "customer", EligibleIsAdult, null);
        host.Rules.Update("can-checkout", CheckoutUsesEligible, 1)
            .Outcome.ShouldBe(RuleUpdateOutcome.Updated);

        var created = host.Changes.Create("alice", "a note",
        [
            new(ChangeTargetKind.Proposition, "customer.eligible", null,
                BaseVersion: 1, RollbackOfVersion: null)
        ]);

        // Act
        var published = host.Changes.Publish(created.Change!.Id, breakGlassActive: false);

        // Assert
        published.Outcome.ShouldBe(ChangeRequestOutcome.Invalid);
        published.Errors.ShouldContain(error => error.Message.Contains("can-checkout"));
        host.Propositions.DocumentJsonOf("customer.eligible")!.ShouldBe(EligibleIsAdult);
    }

    /// <summary>
    /// The mirror of the test above: the same withdrawal is fine when the envelope's own rule edit
    /// is what stops referencing the proposition. The live graph still holds the rule's old edge at
    /// validation time, so reading it alone would refuse a perfectly good envelope.
    /// </summary>
    [Fact]
    public void Should_allow_a_withdrawal_whose_only_referrer_is_redirected_by_the_same_envelope()
    {
        // Arrange
        var host = NewHost();
        host.Propositions.Create("customer.eligible", "customer", EligibleIsAdult, null);
        host.Rules.Update("can-checkout", CheckoutUsesEligible, 1);

        var created = host.Changes.Create("alice", "inline the proposition and retire it",
        [
            new(ChangeTargetKind.Rule, "can-checkout", """{ "rule": { "spec": "customer.is-adult" } }""",
                BaseVersion: 2, RollbackOfVersion: null),
            new(ChangeTargetKind.Proposition, "customer.eligible", null,
                BaseVersion: 1, RollbackOfVersion: null)
        ]);

        // Act
        var published = host.Changes.Publish(created.Change!.Id, breakGlassActive: false);

        // Assert
        published.Outcome.ShouldBe(ChangeRequestOutcome.Ok);
        published.PublishedVersions!["can-checkout"].ShouldBe(3);
        published.PublishedVersions!["customer.eligible"].ShouldBe(0);
        host.Propositions.DocumentJsonOf("customer.eligible").ShouldBeNull();
        host.Rule.Evaluate(new Customer(IsActive: false, Age: 30)).Satisfied.ShouldBeTrue();
    }

    /// <summary>
    /// A rule declared with a *document* default does not leave the dependency graph when it
    /// reverts — it re-acquires its default document's references. Treating a revert as "references
    /// nothing" would clear the only referrer standing between the withdrawal and a dangling name.
    /// </summary>
    [Fact]
    public void Should_refuse_a_withdrawal_whose_referrer_is_a_rule_reverting_to_a_document_default()
    {
        // Arrange — the default document itself references the proposition
        var registry = new SpecRegistry()
            .Register("customer.is-active", IsActive)
            .Register("customer.is-adult", IsAdult);
        var scope = new BindingScope(registry);
        var propositions = new PropositionSet(scope, new InMemoryPropositionStore()).AddModel<Customer>("customer");
        propositions.Create("customer.eligible", "customer", EligibleIsAdult, null)
            .Outcome.ShouldBe(PropositionUpdateOutcome.Created);

        var rule = new AuthoredDefaultRule();
        var rules = new RuleSet(scope).Add(rule);
        rules.Update("can-checkout-authored", """{ "rule": { "spec": "customer.is-active" } }""", 1)
            .Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        var changes = new ChangeRequestSet(new ApprovalGate(), rules, propositions);

        var created = changes.Create("alice", "a note",
        [
            new(ChangeTargetKind.Rule, "can-checkout-authored", null, BaseVersion: 2, RollbackOfVersion: null),
            new(ChangeTargetKind.Proposition, "customer.eligible", null, BaseVersion: 1, RollbackOfVersion: null)
        ]);

        // Act — reverting re-acquires the default's reference to customer.eligible
        var published = changes.Publish(created.Change!.Id, breakGlassActive: false);

        // Assert
        published.Outcome.ShouldBe(ChangeRequestOutcome.Invalid);
        published.FailedTarget.ShouldBe(new ChangeTarget(ChangeTargetKind.Proposition, "customer.eligible"));
        published.Errors.ShouldContain(error => error.Message.Contains("can-checkout-authored"));

        // Nothing applied
        propositions.DocumentJsonOf("customer.eligible")!.ShouldBe(EligibleIsAdult);
        rules.FindEntry("can-checkout-authored")!.Version.ShouldBe(2);
    }

    /// <summary>
    /// A rule and a proposition may share a name. Keying the envelope's republished nodes by bare
    /// name lets the rule's entry mask the same-named proposition, so a live referrer disappears
    /// from the check.
    /// </summary>
    [Fact]
    public void Should_not_let_a_rule_edit_mask_a_same_named_proposition_referrer()
    {
        // Arrange — a proposition named exactly like the rule, and it is what references eligible
        var host = NewHost();
        host.Propositions.Create("customer.eligible", "customer", EligibleIsAdult, null);
        host.Propositions.Create("can-checkout", "customer", CheckoutUsesEligible, null)
            .Outcome.ShouldBe(PropositionUpdateOutcome.Created);

        var created = host.Changes.Create("alice", "a note",
        [
            new(ChangeTargetKind.Rule, "can-checkout", """{ "rule": { "spec": "customer.is-adult" } }""",
                BaseVersion: 1, RollbackOfVersion: null),
            new(ChangeTargetKind.Proposition, "customer.eligible", null, BaseVersion: 1, RollbackOfVersion: null)
        ]);

        // Act — the RULE is edited; the same-named PROPOSITION still references customer.eligible
        var published = host.Changes.Publish(created.Change!.Id, breakGlassActive: false);

        // Assert
        published.Outcome.ShouldBe(ChangeRequestOutcome.Invalid);
        published.Errors.ShouldContain(error => error.Message.Contains("proposition 'can-checkout'"));

        // Nothing applied
        host.Propositions.DocumentJsonOf("customer.eligible")!.ShouldBe(EligibleIsAdult);
        host.Rules.FindEntry("can-checkout")!.Version.ShouldBe(1);
    }

    /// <summary>
    /// A cascade refusal is an ordinary expected outcome — the edit is valid on its own and only a
    /// dependent objects — so it must come back as a value. Binding the document singly would miss
    /// it entirely and let it reach the apply phase, where it can only throw.
    /// </summary>
    [Fact]
    public void Should_return_a_broken_dependent_cascade_as_a_value_rather_than_throwing()
    {
        // Arrange — a policy rule bound through a proposition, which the change turns into a spec
        var registry = new SpecRegistry()
            .Register("customer.is-active-policy", IsActivePolicy)
            .Register("customer.composed", ComposedNonPolicy);
        var scope = new BindingScope(registry);
        var propositions = new PropositionSet(scope, new InMemoryPropositionStore()).AddModel<Customer>("customer");
        propositions.Create("customer.eligible-policy", "customer",
            """{ "rule": { "spec": "customer.is-active-policy" } }""", null);

        var rule = new CanCheckoutPolicyRule();
        var rules = new RuleSet(scope).Add(rule);
        rules.Update("can-checkout-policy", """{ "rule": { "spec": "customer.eligible-policy" } }""", 1)
            .Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        var changes = new ChangeRequestSet(new ApprovalGate(), rules, propositions);

        var created = changes.Create("alice", "a note",
        [
            new(ChangeTargetKind.Proposition, "customer.eligible-policy",
                """{ "rule": { "spec": "customer.composed" } }""",
                BaseVersion: 1, RollbackOfVersion: null)
        ]);

        // Act
        var published = changes.Publish(created.Change!.Id, breakGlassActive: false);

        // Assert — a value, not an exception
        published.Outcome.ShouldBe(ChangeRequestOutcome.Invalid);
        published.FailedTarget.ShouldBe(
            new ChangeTarget(ChangeTargetKind.Proposition, "customer.eligible-policy"));
        published.Errors.ShouldContain(error => error.Code == RuleErrorCode.PolicyRequired);
        published.Errors.ShouldContain(error => error.Message.Contains("can-checkout-policy"));

        // Nothing applied — the rule is still bound to a policy and still evaluates
        propositions.DocumentJsonOf("customer.eligible-policy")!
            .ShouldBe("""{ "rule": { "spec": "customer.is-active-policy" } }""");
        rule.Evaluate(new Customer(IsActive: true, Age: 30)).Satisfied.ShouldBeTrue();
    }

    private sealed record Customer(bool IsActive, int Age);
}
