namespace Motiv.Serialization.Tests.Decisions;

/// <summary>
/// The input-capture seam, and the refusal that makes it a seam rather than a suggestion: a rule
/// cannot be audited unless someone has decided what its records may keep of the model.
/// </summary>
public class DecisionCaptureTests
{
    private sealed class Customer(string id, bool isActive)
    {
        public string Id { get; } = id;
        public bool IsActive { get; } = isActive;
    }

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class CanCheckoutRule() : Rule<Customer, string>("can-checkout", IsActive);

    private const string AuditedDocument =
        """{ "audited": true, "rule": { "spec": "customer.is-active" } }""";

    private static readonly Customer Alice = new("cust-42", isActive: true);

    private static SpecRegistry ARegistry() =>
        new SpecRegistry().Register("customer.is-active", IsActive);

    // --- the three postures ------------------------------------------------------------------

    [Fact]
    public void Should_capture_the_whole_model_under_store_whole()
    {
        // Arrange
        var registry = new DecisionCaptureRegistry().StoreWhole<Customer>();

        // Act
        var input = registry.Capture(Alice).ShouldNotBeNull();

        // Assert
        input.Kind.ShouldBe(DecisionInputKind.Whole);
        input.Value.ShouldBeSameAs(Alice);
    }

    [Fact]
    public void Should_capture_only_the_projection_under_redact()
    {
        // Arrange
        // Substring, not a range: this project also targets net472, which has no System.Range.
        var registry = new DecisionCaptureRegistry().Redact<Customer>(c => c.Id.Substring(0, 4));

        // Act
        var input = registry.Capture(Alice).ShouldNotBeNull();

        // Assert — the mask is the replay ceiling, and nothing outside it reaches the record
        input.Kind.ShouldBe(DecisionInputKind.Redacted);
        input.Value.ShouldBe("cust");
    }

    [Fact]
    public void Should_capture_only_the_key_under_reference_only()
    {
        // Arrange
        var registry = new DecisionCaptureRegistry().ReferenceOnly<Customer>(c => c.Id);

        // Act
        var input = registry.Capture(Alice).ShouldNotBeNull();

        // Assert — erase the subject in the adopter's own store and this record survives without
        // personal data, while replay correctly becomes impossible
        input.Kind.ShouldBe(DecisionInputKind.Reference);
        input.Value.ShouldBe("cust-42");
    }

    [Fact]
    public void Should_capture_nothing_for_an_unregistered_model_type()
    {
        // Act
        var input = new DecisionCaptureRegistry().Capture(Alice);

        // Assert
        input.ShouldBeNull();
    }

    [Fact]
    public void Should_let_the_last_posture_registered_for_a_model_type_win()
    {
        // Arrange — a host tightening its posture must not have to unregister the old one
        var registry = new DecisionCaptureRegistry()
            .StoreWhole<Customer>()
            .ReferenceOnly<Customer>(c => c.Id);

        // Act
        var input = registry.Capture(Alice).ShouldNotBeNull();

        // Assert
        input.Kind.ShouldBe(DecisionInputKind.Reference);
    }

    // --- the refusal -------------------------------------------------------------------------

    [Fact]
    public async Task Should_refuse_to_bind_an_audited_rule_when_no_capture_posture_covers_its_model()
    {
        // Arrange — a log exists, but nothing was decided about Customer
        await using var log = new DecisionLog(new InMemoryDecisionSink());
        var rules = new RuleSet(ARegistry(), decisionLog: log).Add(new CanCheckoutRule());

        // Act
        var result = await rules.UpdateAsync(
            "can-checkout", AuditedDocument, expectedVersion: 1, new RuleChangeProvenance("alice"));

        // Assert — a whole-model default that is on by omission is the trap this refusal exists for
        result.Outcome.ShouldBe(RuleUpdateOutcome.Invalid);
        var error = result.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.AuditCaptureNotConfigured);
        error.Path.ShouldBe("$.audited");
    }

    [Fact]
    public async Task Should_refuse_to_bind_an_audited_rule_when_no_decision_log_is_configured()
    {
        // Arrange — no log at all, which is the same fail-closed case with a different cause
        var rules = new RuleSet(ARegistry()).Add(new CanCheckoutRule());

        // Act
        var result = await rules.UpdateAsync(
            "can-checkout", AuditedDocument, expectedVersion: 1, new RuleChangeProvenance("alice"));

        // Assert
        result.Outcome.ShouldBe(RuleUpdateOutcome.Invalid);
        result.Errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.AuditCaptureNotConfigured);
    }

    [Fact]
    public async Task Should_bind_an_audited_rule_when_a_posture_covers_its_model()
    {
        // Arrange
        await using var log = new DecisionLog(new InMemoryDecisionSink());
        log.Capture.ReferenceOnly<Customer>(c => c.Id);
        var rules = new RuleSet(ARegistry(), decisionLog: log).Add(new CanCheckoutRule());

        // Act
        var result = await rules.UpdateAsync(
            "can-checkout", AuditedDocument, expectedVersion: 1, new RuleChangeProvenance("alice"));

        // Assert
        result.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
    }

    [Fact]
    public async Task Should_leave_an_unaudited_rule_alone_when_no_capture_is_configured()
    {
        // Arrange — the refusal must be about auditing, not about having a decision log at all
        var rules = new RuleSet(ARegistry()).Add(new CanCheckoutRule());

        // Act
        var result = await rules.UpdateAsync(
            "can-checkout", """{ "rule": { "spec": "customer.is-active" } }""",
            expectedVersion: 1, new RuleChangeProvenance("alice"));

        // Assert
        result.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
    }

    [Fact]
    public async Task Should_quarantine_an_audited_stored_rule_a_replica_has_no_capture_for()
    {
        // Arrange — one replica published the audited document; this one was deployed without the
        // posture that made it publishable
        var store = new InMemoryRuleStore();
        await store.AppendAsync(
            [new StoredRuleVersion("can-checkout", 2, AuditedDocument, "alice", DateTimeOffset.UtcNow, null, null, "build")],
            CancellationToken.None);

        var rules = new RuleSet(ARegistry(), store).Add(new CanCheckoutRule());

        // Act
        var report = rules.Load();

        // Assert — reported and quarantined, not fatal and not silently unaudited
        var quarantined = report.Quarantined.ShouldHaveSingleItem();
        quarantined.Name.ShouldBe("can-checkout");
        quarantined.Errors.ShouldContain(error => error.Code == RuleErrorCode.AuditCaptureNotConfigured);
    }
}
