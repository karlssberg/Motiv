using Motiv.Diagnostics;

namespace Motiv.Serialization.Tests.Diagnostics;

/// <summary>
/// Per-node spans: three gates that must all be open, a bound that reports itself, and the same PII
/// control the evaluation span obeys.
/// </summary>
[Collection(RulesTelemetryTestCollection.Name)]
public class NodeSpanTests
{
    private sealed class Customer(bool isActive, int age)
    {
        public bool IsActive { get; } = isActive;
        public int Age { get; } = age;
    }

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static SpecBase<Customer, string> IsAdult { get; } =
        Spec.Build((Customer c) => c.Age >= 18).WhenTrue("adult").WhenFalse("minor").Create();

    private const string AuditedBoth =
        """{"audited": true, "rule": {"and": [{"spec": "customer.is-active"}, {"spec": "customer.is-adult"}]}}""";

    private const string UnauditedBoth =
        """{"rule": {"and": [{"spec": "customer.is-active"}, {"spec": "customer.is-adult"}]}}""";

    private sealed class CanCheckoutRule() : Rule<Customer, string>("can-checkout", IsActive);

    /// <summary>A rule bound to the given document, with the capture posture an audited rule needs.</summary>
    /// <remarks>
    /// Hands back the decision log so the caller can dispose it. A log left running stays registered
    /// with the rules meter and goes on contributing readings to every later test's poll, which is
    /// exactly the kind of cross-test noise that makes a suite fail only when run whole.
    /// </remarks>
    private static async Task<(CanCheckoutRule Rule, DecisionLog Log)> BoundTo(string documentJson)
    {
        var registry = new SpecRegistry()
            .Register("customer.is-active", IsActive)
            .Register("customer.is-adult", IsAdult);

        var options = new DecisionLogOptions();
        options.Capture.StoreWhole<Customer>();
        var log = new DecisionLog(new NullSink(), options);

        var rule = new CanCheckoutRule();
        var rules = new RuleSet(registry, new InMemoryRuleStore(), decisionLog: log).Add(rule);
        rules.Load();
        await rules.UpdateAsync("can-checkout", documentJson, 1, new RuleChangeProvenance("alice"));
        return (rule, log);
    }

    private sealed class NullSink : IDecisionSink
    {
        public Task WriteAsync(IReadOnlyList<DecisionRecord> records, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task WriteGapAsync(DecisionGap gap, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    /// <summary>Runs <paramref name="body"/> with node spans on, restoring the process-wide switches after.</summary>
    /// <remarks>
    /// Sets <see cref="MotivTelemetry.ExplanationDetail"/> rather than merely restoring it, because
    /// the coupling this slice adds deliberately <em>never loosens</em>: any decision log created
    /// anywhere in the process with a <c>Redact</c> or <c>ReferenceOnly</c> posture tightens it to
    /// <see cref="ExplanationDetail.None"/> for good. That is right for a host, where the log is
    /// created once at startup, and it means a test wanting explanation text has to say so rather
    /// than inherit whatever ran before it.
    /// </remarks>
    private static async Task WithNodeSpans(Func<Task> body, int max = 1000)
    {
        var previousOn = MotivRulesTelemetry.NodeSpans;
        var previousMax = MotivRulesTelemetry.MaxNodeSpans;
        var previousDetail = MotivTelemetry.ExplanationDetail;
        MotivRulesTelemetry.NodeSpans = true;
        MotivRulesTelemetry.MaxNodeSpans = max;
        MotivTelemetry.ExplanationDetail = ExplanationDetail.Full;
        try
        {
            await body();
        }
        finally
        {
            MotivRulesTelemetry.NodeSpans = previousOn;
            MotivRulesTelemetry.MaxNodeSpans = previousMax;
            MotivTelemetry.ExplanationDetail = previousDetail;
        }
    }

    [Fact]
    public async Task Should_emit_a_span_per_causal_node_of_an_audited_rule()
    {
        await WithNodeSpans(async () =>
        {
            // Arrange
            var (rule, log) = await BoundTo(AuditedBoth);
            await using var _ = log;
            using var harness = new RulesTelemetryHarness();

            // Act — both operands satisfied, so both are causal.
            rule.Evaluate(new Customer(isActive: true, age: 30)).Satisfied.ShouldBeTrue();

            // Assert
            var nodes = harness.Activities.Where(a => a.OperationName == "motiv.rules.node").ToList();
            nodes.Count.ShouldBe(2);
            nodes.ShouldAllBe(node => (bool)node.GetTagItem("motiv.satisfied")!);
            nodes.Select(node => (string)node.GetTagItem("motiv.reason")!)
                .ShouldBe(["active", "adult"], ignoreOrder: true);
        });
    }

    [Fact]
    public async Task Should_nest_the_node_spans_under_the_rule_span()
    {
        await WithNodeSpans(async () =>
        {
            // Arrange
            var (rule, log) = await BoundTo(AuditedBoth);
            await using var _ = log;
            using var harness = new RulesTelemetryHarness();

            // Act
            rule.Evaluate(new Customer(isActive: true, age: 30));

            // Assert — the shape is the point: a node span outside the rule span would name a
            // sub-proposition without saying which evaluation it belonged to.
            var evaluation = harness.SingleActivity("motiv.rules.evaluate");
            harness.Activities.Where(a => a.OperationName == "motiv.rules.node")
                .ShouldAllBe(node => node.ParentSpanId == evaluation.SpanId);
        });
    }

    [Fact]
    public async Task Should_emit_nothing_for_a_rule_that_is_not_audited()
    {
        await WithNodeSpans(async () =>
        {
            // Arrange
            var (rule, log) = await BoundTo(UnauditedBoth);
            await using var _ = log;
            using var harness = new RulesTelemetryHarness();

            // Act
            rule.Evaluate(new Customer(isActive: true, age: 30));

            // Assert — node spans ride the audited flag, so a rule nobody agreed to record does not
            // start emitting its internals because a process-wide switch was flipped.
            harness.Activities.ShouldNotContain(a => a.OperationName == "motiv.rules.node");
            harness.Activities.ShouldContain(a => a.OperationName == "motiv.rules.evaluate");
        });
    }

    [Fact]
    public async Task Should_emit_nothing_while_the_switch_is_off()
    {
        // Arrange — audited, but node spans left at their default.
        MotivRulesTelemetry.NodeSpans.ShouldBeFalse("node spans must be off by default");
        var (rule, log) = await BoundTo(AuditedBoth);
        await using var _ = log;
        using var harness = new RulesTelemetryHarness();

        // Act
        rule.Evaluate(new Customer(isActive: true, age: 30));

        // Assert
        harness.Activities.ShouldNotContain(a => a.OperationName == "motiv.rules.node");
    }

    [Fact]
    public async Task Should_say_so_when_the_tree_is_larger_than_the_bound()
    {
        await WithNodeSpans(
            async () =>
            {
                // Arrange
                var (rule, log) = await BoundTo(AuditedBoth);
                await using var _ = log;
                using var harness = new RulesTelemetryHarness();

                // Act
                rule.Evaluate(new Customer(isActive: true, age: 30));

                // Assert — a waterfall that stops short silently reads as a complete picture of a
                // smaller tree.
                harness.Activities.Count(a => a.OperationName == "motiv.rules.node").ShouldBe(1);
                harness.SingleActivity("motiv.rules.evaluate")
                    .GetTagItem("motiv.rules.nodes.truncated").ShouldBe(true);
            },
            max: 1);
    }

    [Fact]
    public async Task Should_carry_no_explanation_text_when_the_capture_posture_forbids_it()
    {
        await WithNodeSpans(async () =>
        {
            // Arrange
            var (rule, log) = await BoundTo(AuditedBoth);
            await using var _ = log;
            MotivTelemetry.ExplanationDetail = ExplanationDetail.None;
            using var harness = new RulesTelemetryHarness();

            // Act
            rule.Evaluate(new Customer(isActive: true, age: 30));

            // Assert — one span per node is the widest exposure of assertion text anywhere, so this
            // is exactly where the coupling has to hold.
            var nodes = harness.Activities.Where(a => a.OperationName == "motiv.rules.node").ToList();
            nodes.Count.ShouldBe(2);
            nodes.ShouldAllBe(node => node.GetTagItem("motiv.reason") == null);
            nodes.ShouldAllBe(node => node.GetTagItem("motiv.satisfied") != null);
        });
    }

    [Fact]
    public void Should_refuse_a_bound_that_would_emit_nothing()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => MotivRulesTelemetry.MaxNodeSpans = 0);
    }
}
