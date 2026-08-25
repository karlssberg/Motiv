using System.Diagnostics;

namespace Motiv.Serialization.Tests.Diagnostics;

/// <summary>
/// The pivot from a publish to the evaluations that ran what was published.
/// </summary>
/// <remarks>
/// Covers all four evaluation entry points, because two of them
/// (<see cref="PolicyRule{TModel,TMetadata}.Evaluate"/> and
/// <see cref="AsyncPolicyRule{TModel,TMetadata}.EvaluateAsync"/>) are <em>shadows</em> rather than
/// overrides — the shape that gets half-instrumented, and a rule that tags one of its two methods is
/// worse than one that tags neither, because the gap only shows up as a trace that inexplicably has
/// no version on it.
/// </remarks>
[Collection(RulesTelemetryTestCollection.Name)]
public class RuleSpanCorrelationTests
{
    private static SpecBase<int, string> Positive { get; } = Spec.Build((int n) => n > 0).Create("positive");

    private static PolicyBase<int, string> PositivePolicy { get; } =
        Spec.Build((int n) => n > 0).WhenTrue("positive").WhenFalse("not positive").Create();

    private static AsyncSpecBase<int, string> PositiveAsync { get; } =
        Spec.BuildAsync(async (int n) => { await Task.Yield(); return n > 0; }).Create("positive-async");

    private static AsyncPolicyBase<int, string> PositiveAsyncPolicy { get; } =
        Spec.BuildAsync(async (int n) => { await Task.Yield(); return n > 0; })
            .WhenTrue("positive").WhenFalse("not positive").Create();

    private sealed class NumberRule() : Rule<int, string>("number", Positive);

    private sealed class NumberPolicyRule() : PolicyRule<int, string>("number-policy", PositivePolicy);

    private sealed class NumberAsyncRule() : AsyncRule<int, string>("number-async", PositiveAsync);

    private sealed class NumberAsyncPolicyRule()
        : AsyncPolicyRule<int, string>("number-async-policy", PositiveAsyncPolicy);

    /// <summary>Every way a named rule can be evaluated, and the name each one carries.</summary>
    public static TheoryData<string> EveryEntryPoint =>
        ["number", "number-policy", "number-async", "number-async-policy"];

    private static readonly IReadOnlyDictionary<string, Func<RuleSet, Task>> Evaluations =
        new Dictionary<string, Func<RuleSet, Task>>(StringComparer.Ordinal)
        {
            ["number"] = rules =>
            {
                ((NumberRule)rules.Find("number")!).Evaluate(1);
                return Task.CompletedTask;
            },
            ["number-policy"] = rules =>
            {
                ((NumberPolicyRule)rules.Find("number-policy")!).Evaluate(1);
                return Task.CompletedTask;
            },
            ["number-async"] = async rules =>
                await ((NumberAsyncRule)rules.Find("number-async")!).EvaluateAsync(1),
            ["number-async-policy"] = async rules =>
                await ((NumberAsyncPolicyRule)rules.Find("number-async-policy")!).EvaluateAsync(1)
        };

    private static RuleSet NewRules() =>
        new RuleSet(
                new SpecRegistry()
                    .Register("positive", Positive)
                    .Register("positive-async", PositiveAsync))
            .Add(new NumberRule())
            .Add(new NumberPolicyRule())
            .Add(new NumberAsyncRule())
            .Add(new NumberAsyncPolicyRule());

    [Theory]
    [MemberData(nameof(EveryEntryPoint))]
    public async Task Should_tag_the_evaluation_with_the_rule_name_and_version(string ruleName)
    {
        // Arrange
        var rules = NewRules();
        using var harness = new RulesTelemetryHarness();

        // Act
        await Evaluations[ruleName](rules);

        // Assert
        var span = harness.SingleActivity("motiv.rules.evaluate");
        span.GetTagItem("motiv.rules.name").ShouldBe(ruleName);
        span.GetTagItem("motiv.rules.version").ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(EveryEntryPoint))]
    public async Task Should_parent_the_core_evaluation_span_so_core_need_not_know_about_versions(string ruleName)
    {
        // Arrange
        var rules = NewRules();
        using var harness = new RulesTelemetryHarness(listenToCore: true);

        // Act
        await Evaluations[ruleName](rules);

        // Assert — a SpecBase has no version, and giving it one to satisfy an operator's query would
        // push a rules-stack concern into the published engine. Containment answers the same question.
        //
        // Asserted on Parent and Id, never on SpanId/TraceId: those are populated only under the W3C
        // id format, and .NET Framework still defaults to the older hierarchical one, where both
        // sides read as all-zeros and the assertion passes without having compared anything.
        var rule = harness.SingleActivity("motiv.rules.evaluate");
        var core = harness.SingleActivity("motiv.evaluate");
        core.Parent.ShouldBeSameAs(rule);
        rule.Id.ShouldNotBeNullOrEmpty();
        core.ParentId!.ShouldBe(rule.Id!);
    }

    [Fact]
    public async Task Should_move_the_version_tag_when_the_rule_is_republished()
    {
        // Arrange
        var rules = new RuleSet(new SpecRegistry().Register("positive", Positive), new InMemoryRuleStore())
            .Add(new NumberRule());
        rules.Load();
        await rules.UpdateAsync("number", """{"rule":{"not":{"spec":"positive"}}}""", 1, new RuleChangeProvenance("alice"));

        using var harness = new RulesTelemetryHarness();

        // Act
        ((NumberRule)rules.Find("number")!).Evaluate(1);

        // Assert — this is the whole point: an operator holding a publish can find the evaluations
        // that ran the version it produced.
        harness.SingleActivity("motiv.rules.evaluate").GetTagItem("motiv.rules.version").ShouldBe(2);
    }

    [Fact]
    public void Should_open_no_span_when_nothing_is_listening()
    {
        // Arrange — no harness, so the rules source has no listener.
        var rules = NewRules();

        // Act
        ((NumberRule)rules.Find("number")!).Evaluate(1);

        // Assert — an unobserved evaluation must not allocate an Activity or leave Current set.
        Activity.Current.ShouldBeNull();
    }
}
