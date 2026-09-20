using System.Text.Json;
using Motiv;
using Motiv.Serialization;
using Motiv.Serialization.Snapshots;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.Snapshots.Tests;

/// <summary>
/// A snapshot binds exactly as the reproduction that produced it did: its proposition rows shadow
/// whatever the registry has moved on to, in dependency order, over the compiled specs.
/// </summary>
public class RuleSnapshotTests
{
    private sealed record Customer(string Id, bool IsActive, int Age);

    private static PolicyBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();
    private static PolicyBase<Customer, string> IsAdult { get; } =
        Spec.Build((Customer c) => c.Age >= 18).WhenTrue("adult").WhenFalse("minor").Create();

    private sealed class CanCheckout() : Rule<Customer, string>("can-checkout", IsActive);

    private const string AuditedOverEligible = """{ "audited": true, "rule": { "spec": "customer.eligible" } }""";

    private static SpecRegistry Registry() =>
        new SpecRegistry().Register("customer.is-active", IsActive).Register("customer.is-adult", IsAdult);

    private static string Row(string name, int version, string modelType, string document) =>
        JsonSerializer.Serialize(new { name, version, modelType, document = JsonDocument.Parse(document).RootElement });

    /// <summary>Decides for an active minor under eligible v1 (is-active), publishes v2 (is-adult), reproduces.</summary>
    private static async Task<Reproduction> AReproductionAsync()
    {
        var sink = new InMemoryDecisionSink();
        var options = new DecisionLogOptions { Backpressure = DecisionBackpressure.Block };
        options.Capture.StoreWhole<Customer>();
        await using var log = new DecisionLog(sink, options);
        var propositionStore = new InMemoryPropositionStore();
        var propositions = new PropositionSet(Registry(), propositionStore).AddModel<Customer>("customer");
        propositions.Load();
        var ruleStore = new InMemoryRuleStore();
        var rules = new RuleSet(propositions, ruleStore, decisionLog: log).Add(new CanCheckout());
        (await propositions.CreateAsync("customer.eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null)).Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        (await rules.UpdateAsync("can-checkout", AuditedOverEligible, 1, new RuleChangeProvenance("alice"))).Outcome.ShouldBe(RuleUpdateOutcome.Updated);

        ((CanCheckout)rules.Find("can-checkout")!).Evaluate(new Customer("cust-7", IsActive: true, Age: 16));
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (sink.Records.Count == 0 && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        (await propositions.UpdateAsync("customer.eligible", """{ "rule": { "spec": "customer.is-adult" } }""", 1)).Outcome.ShouldBe(PropositionUpdateOutcome.Updated);

        var reproducer = new DecisionReproducer(sink, ruleStore, propositionStore, rules, propositions, options.Resolve, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return await reproducer.ReproduceAsync(sink.Records[0].Id, default);
    }

    [Fact]
    public async Task Should_decide_as_the_reproducer_for_the_same_snapshot()
    {
        // Arrange — the reproduction's rows, and a registry whose head for eligible has moved on
        var reproduction = await AReproductionAsync();
        reproduction.Fidelity.IsExact.ShouldBeTrue(string.Join("; ", reproduction.Fidelity.Notes));
        var snapshot = RuleSnapshot.FromJson(
            reproduction.Rule!.DocumentJson!,
            reproduction.Propositions.Select(p => Row(p.Name, p.Version, p.ModelType!, p.DocumentJson!)));
        var movedOn = Registry().Register("customer.eligible", IsAdult);

        // Act
        var bound = snapshot.Bind<Customer>(movedOn);
        var result = bound.Evaluate(new Customer("cust-7", IsActive: true, Age: 16));

        // Assert — v1 (is-active) decided yes; today's head would say no
        snapshot.Warnings.ShouldBeEmpty();
        result.Satisfied.ShouldBe(reproduction.Replayed!.Satisfied);
        result.Assertions.ShouldBe(reproduction.Replayed.Assertions, ignoreOrder: true);
        result.Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_refuse_rows_over_several_model_types()
    {
        var snapshot = RuleSnapshot.FromJson(
            """{ "rule": { "spec": "a" } }""",
            [Row("a", 1, "customer", """{ "rule": { "spec": "customer.is-active" } }"""), Row("b", 1, "order", """{ "rule": { "spec": "customer.is-active" } }""")]);

        Should.Throw<InvalidOperationException>(() => snapshot.Bind<Customer>(Registry())).Message.ShouldContain("order");
    }

    [Fact]
    public void Should_report_a_row_that_does_not_bind_as_a_warning_and_still_bind_the_rule()
    {
        var snapshot = RuleSnapshot.FromJson(
            """{ "rule": { "spec": "customer.is-active" } }""",
            [Row("customer.broken", 3, "customer", """{ "rule": { "spec": "customer.gone" } }""")]);

        var bound = snapshot.Bind<Customer>(Registry());

        snapshot.Warnings.ShouldHaveSingleItem().ShouldContain("customer.broken");
        bound.Evaluate(new Customer("x", true, 30)).Satisfied.ShouldBeTrue();
    }
}
