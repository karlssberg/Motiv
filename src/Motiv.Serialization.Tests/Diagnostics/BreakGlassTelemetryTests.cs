namespace Motiv.Serialization.Tests.Diagnostics;

/// <summary>
/// Break-glass is loud by design — a deploy-time flag that disables the approval gate. These are the
/// two halves of "loud": that it is on, and what went out while it was.
/// </summary>
[Collection(RulesTelemetryTestCollection.Name)]
public class BreakGlassTelemetryTests
{
    private sealed class Customer(bool isActive)
    {
        public bool IsActive { get; } = isActive;
    }

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    /// <summary>
    /// Maker-checker: one approver, and not the author. A freshly created request has no approvers,
    /// so this blocks it — and anything that publishes anyway did so by bypassing the gate.
    /// </summary>
    private const string MakerChecker =
        """
        {"rule": {"and": [
            {"spec": "change.approver-count-at-least", "args": {"n": 1}},
            {"not": {"spec": "change.author-is-approver"}}
        ]}}
        """;
    private const string NotActive = """{"rule": {"not": {"spec": "customer.is-active"}}}""";

    private sealed class CanCheckoutRule() : Rule<Customer, string>("can-checkout", IsActive);

    private static (ChangeRequestSet Changes, RuleSet Rules) NewHost()
    {
        var scope = new BindingScope(new SpecRegistry().Register("customer.is-active", IsActive));
        var propositions = new PropositionSet(scope, new InMemoryPropositionStore()).AddModel<Customer>("customer");
        var rules = new RuleSet(scope).Add(new CanCheckoutRule());

        // A gate an unapproved request cannot satisfy, so anything that publishes bypassed it.
        var gate = new ApprovalGate();
        gate.SetGate(MakerChecker, ["motiv-dev"]).Outcome.ShouldBe(GateUpdateOutcome.Updated);

        return (new ChangeRequestSet(gate, rules, propositions), rules);
    }

    private static NewProposedChange ARuleEdit() =>
        new(ChangeTargetKind.Rule, "can-checkout", NotActive, BaseVersion: 1, RollbackOfVersion: null);

    [Fact]
    public void Should_report_a_break_glass_window_as_active_while_it_is_open()
    {
        // Arrange
        using var harness = new RulesTelemetryHarness();
        var open = new BreakGlass(enabled: true, expiresUtc: DateTimeOffset.UtcNow.AddHours(1));

        // Act
        harness.Collect();

        // Assert
        open.Active(DateTimeOffset.UtcNow).ShouldBeTrue();
        harness.For("motiv.rules.break_glass.active").ShouldContain(m => m.Value == 1);
    }

    [Fact]
    public void Should_report_an_expired_window_as_no_longer_active()
    {
        // Arrange — a forgotten break-glass that has timed out is exactly what the expiry is for.
        using var harness = new RulesTelemetryHarness();
        var expired = new BreakGlass(enabled: true, expiresUtc: DateTimeOffset.UtcNow.AddHours(-1));

        // Act
        harness.Collect();

        // Assert — reported, and reported as zero. A series that vanished would be indistinguishable
        // from a replica whose meter stopped answering. Asserted as "contains", not "all": every
        // window still reachable anywhere in the process reports, this one included.
        expired.Active(DateTimeOffset.UtcNow).ShouldBeFalse();
        harness.For("motiv.rules.break_glass.active").ShouldContain(m => m.Value == 0);
    }

    [Fact]
    public async Task Should_count_a_governed_publish_that_bypassed_the_gate()
    {
        // Arrange
        var (changes, _) = NewHost();
        var created = changes.Create("alice", "urgent", [ARuleEdit()]);
        using var harness = new RulesTelemetryHarness();

        // Act
        var published = await changes.PublishAsync(created.Change!.Id, breakGlassActive: true);

        // Assert
        published.Change!.PublishedUnderBreakGlass.ShouldBeTrue();
        var counted = harness.Single("motiv.rules.publishes_under_break_glass");
        counted.Value.ShouldBe(1);
        counted.Tag("motiv.rules.kind").ShouldBe("rule");
    }

    [Fact]
    public async Task Should_not_count_a_publish_the_gate_actually_allowed()
    {
        // Arrange — a permissive gate, and no break-glass.
        var scope = new BindingScope(new SpecRegistry().Register("customer.is-active", IsActive));
        var propositions = new PropositionSet(scope, new InMemoryPropositionStore()).AddModel<Customer>("customer");
        var rules = new RuleSet(scope).Add(new CanCheckoutRule());
        var changes = new ChangeRequestSet(new ApprovalGate(), rules, propositions);
        var created = changes.Create("alice", "ordinary", [ARuleEdit()]);

        using var harness = new RulesTelemetryHarness();

        // Act
        var published = await changes.PublishAsync(created.Change!.Id, breakGlassActive: false);

        // Assert
        published.Outcome.ShouldBe(ChangeRequestOutcome.Ok);
        harness.For("motiv.rules.publishes_under_break_glass").ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_not_count_a_bypass_whose_write_then_failed()
    {
        // Arrange — break-glass skips the gate, but a stale base version still fails the CAS.
        var (changes, _) = NewHost();
        var stale = new NewProposedChange(
            ChangeTargetKind.Rule, "can-checkout", NotActive, BaseVersion: 99, RollbackOfVersion: null);
        var created = changes.Create("alice", "urgent", [stale]);

        using var harness = new RulesTelemetryHarness();

        // Act
        var published = await changes.PublishAsync(created.Change!.Id, breakGlassActive: true);

        // Assert — break-glass says the ceremony was skipped, not that anything went live. Counting
        // here would put a publish on the dashboard that never happened.
        published.Outcome.ShouldNotBe(ChangeRequestOutcome.Ok);
        harness.For("motiv.rules.publishes_under_break_glass").ShouldBeEmpty();
    }
}
