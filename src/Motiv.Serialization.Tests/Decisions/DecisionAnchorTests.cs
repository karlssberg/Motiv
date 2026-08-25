namespace Motiv.Serialization.Tests.Decisions;

/// <summary>
/// The third anchor: what the names a rule resolves through <em>meant</em> when it ran. A rule
/// version pins the rule's own composition and says nothing about its propositions, which is why the
/// pin is a separate anchor — and why it has to be the transitive closure rather than the first hop.
/// </summary>
// Constructing a DecisionLog can tighten process-wide explanation detail — see
// RulesTelemetryTestCollection for why that makes this class un-parallelizable.
[Collection(Diagnostics.RulesTelemetryTestCollection.Name)]
public class DecisionAnchorTests
{
    private sealed class Customer(string id, bool isActive, int age)
    {
        public string Id { get; } = id;
        public bool IsActive { get; } = isActive;
        public int Age { get; } = age;
    }

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static SpecBase<Customer, string> IsAdult { get; } =
        Spec.Build((Customer c) => c.Age >= 18).WhenTrue("adult").WhenFalse("minor").Create();

    private sealed class CanCheckoutRule() : Rule<Customer, string>("can-checkout", IsActive);

    private static readonly Customer Alice = new("cust-42", isActive: true, age: 30);

    /// <summary>
    /// A rule reaching <c>customer.is-active</c> only through <c>pricing.eligible</c>: two hops, so a
    /// pin that stopped at the first would miss the proposition that actually decided the outcome.
    /// </summary>
    private static async Task<(PropositionSet Propositions, RuleSet Rules, InMemoryDecisionSink Sink, DecisionLog Log)>
        ATwoHopHostAsync()
    {
        var registry = new SpecRegistry()
            .Register("customer.is-active", IsActive)
            .Register("customer.is-adult", IsAdult);

        var propositions = new PropositionSet(registry, new InMemoryPropositionStore())
            .AddModel<Customer>("customer");
        propositions.Load();

        (await propositions.CreateAsync(
            "customer.eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null))
            .Outcome.ShouldBe(PropositionUpdateOutcome.Created);

        (await propositions.CreateAsync(
            "pricing.eligible", "customer", """{ "rule": { "spec": "customer.eligible" } }""", null))
            .Outcome.ShouldBe(PropositionUpdateOutcome.Created);

        var sink = new InMemoryDecisionSink();
        var options = new DecisionLogOptions { Backpressure = DecisionBackpressure.Block };
        options.Capture.ReferenceOnly<Customer>(c => c.Id);
        var log = new DecisionLog(sink, options);

        var rules = new RuleSet(propositions, decisionLog: log).Add(new CanCheckoutRule());
        (await rules.UpdateAsync(
            "can-checkout",
            """{ "audited": true, "rule": { "spec": "pricing.eligible" } }""",
            expectedVersion: 1,
            new RuleChangeProvenance("alice")))
            .Outcome.ShouldBe(RuleUpdateOutcome.Updated);

        return (propositions, rules, sink, log);
    }

    [Fact]
    public async Task Should_pin_every_proposition_the_rule_reaches_transitively()
    {
        // Arrange
        var (_, rules, sink, log) = await ATwoHopHostAsync();
        await using var _log = log;

        // Act
        ((CanCheckoutRule)rules.Find("can-checkout")!).Evaluate(Alice);
        await log.DisposeAsync();

        // Assert — both hops, not just the one the document names
        var record = sink.Records.ShouldHaveSingleItem();
        record.ReferencedPropositionVersions.Select(pin => pin.Name)
            .ShouldBe(["pricing.eligible", "customer.eligible"], ignoreOrder: true);
        record.ReferencedPropositionVersions.ShouldAllBe(pin => pin.Version == 1);
    }

    [Fact]
    public async Task Should_move_the_pin_when_a_proposition_two_hops_down_is_republished()
    {
        // Arrange
        var (propositions, rules, sink, log) = await ATwoHopHostAsync();
        await using var _log = log;
        var rule = (CanCheckoutRule)rules.Find("can-checkout")!;
        rule.Evaluate(Alice);

        // Act — republish the *far* proposition; the rule's own document never changes
        (await propositions.UpdateAsync(
            "customer.eligible",
            """{ "rule": { "and": [ { "spec": "customer.is-active" }, { "spec": "customer.is-adult" } ] } }""",
            expectedVersion: 1))
            .Outcome.ShouldBe(PropositionUpdateOutcome.Updated);
        rule.Evaluate(Alice);
        await log.DisposeAsync();

        // Assert — this is what makes computing the pin once per bound state sound rather than a
        // shortcut: republishing anything in the closure rebinds the referrer and produces a new
        // state, so a pin cached against a state can never go stale
        sink.Records.Count.ShouldBe(2);

        var before = sink.Records[0].ReferencedPropositionVersions
            .Single(pin => pin.Name == "customer.eligible");
        var after = sink.Records[1].ReferencedPropositionVersions
            .Single(pin => pin.Name == "customer.eligible");

        before.Version.ShouldBe(1);
        after.Version.ShouldBe(2);

        // ...and the rule's own version did not move, which is why one anchor could never do the job
        sink.Records[0].RuleVersion.ShouldBe(sink.Records[1].RuleVersion);
    }

    [Fact]
    public async Task Should_pin_nothing_for_a_rule_that_resolves_only_compiled_specs()
    {
        // Arrange — a compiled spec has no version of its own, which is what BuildId is for
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var sink = new InMemoryDecisionSink();
        var options = new DecisionLogOptions();
        options.Capture.ReferenceOnly<Customer>(c => c.Id);
        await using var log = new DecisionLog(sink, options);

        var rules = new RuleSet(registry, decisionLog: log).Add(new CanCheckoutRule());
        await rules.UpdateAsync(
            "can-checkout",
            """{ "audited": true, "rule": { "spec": "customer.is-active" } }""",
            expectedVersion: 1,
            new RuleChangeProvenance("alice"));

        // Act
        ((CanCheckoutRule)rules.Find("can-checkout")!).Evaluate(Alice);
        await log.DisposeAsync();

        // Assert
        var record = sink.Records.ShouldHaveSingleItem();
        record.ReferencedPropositionVersions.ShouldBeEmpty();
        record.BuildId.ShouldBe(BuildIdentity.Current);
    }
}
