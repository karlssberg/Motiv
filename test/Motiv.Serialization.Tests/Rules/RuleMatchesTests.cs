namespace Motiv.Serialization.Tests.Rules;

/// <summary>
/// The boolean-only entry point on a live rule — what a flag call site asks when it wants an answer
/// and not an explanation. Covers all four flavours, because the policy flavours inherit it rather
/// than shadow it, and an inheritance that silently evaluated the wrong spec would still compile.
/// </summary>
public class RuleMatchesTests
{
    private static PolicyBase<int, string> Positive { get; } =
        Spec.Build((int n) => n > 0).WhenTrue("positive").WhenFalse("not positive").Create();

    private static AsyncPolicyBase<int, string> PositiveAsync { get; } =
        Spec.BuildAsync(async (int n) => { await Task.Yield(); return n > 0; })
            .WhenTrue("positive").WhenFalse("not positive").Create();

    private sealed class NumberRule() : Rule<int, string>("number", Positive);

    private sealed class NumberPolicyRule() : PolicyRule<int, string>("number-policy", Positive);

    private sealed class NumberAsyncRule() : AsyncRule<int, string>("number-async", PositiveAsync);

    private sealed class NumberAsyncPolicyRule() : AsyncPolicyRule<int, string>("number-async-policy", PositiveAsync);

    private static RuleSet NewRules() =>
        new RuleSet(new SpecRegistry().Register("positive", Positive).Register("positive-async", PositiveAsync))
            .Add(new NumberRule())
            .Add(new NumberPolicyRule())
            .Add(new NumberAsyncRule())
            .Add(new NumberAsyncPolicyRule());

    /// <summary>Every flavour, crossed with a model that satisfies it and one that does not.</summary>
    public static TheoryData<string, int> EveryFlavourAndOutcome => new()
    {
        { "number", 1 }, { "number", -1 },
        { "number-policy", 1 }, { "number-policy", -1 },
        { "number-async", 1 }, { "number-async", -1 },
        { "number-async-policy", 1 }, { "number-async-policy", -1 }
    };

    private static async Task<(bool Matched, bool Satisfied)> MatchAndEvaluate(RuleSet rules, string name, int model) =>
        rules.Find(name) switch
        {
            NumberAsyncPolicyRule rule => (await rule.MatchesAsync(model), (await rule.EvaluateAsync(model)).Satisfied),
            NumberAsyncRule rule => (await rule.MatchesAsync(model), (await rule.EvaluateAsync(model)).Satisfied),
            NumberPolicyRule rule => (rule.Matches(model), rule.Evaluate(model).Satisfied),
            NumberRule rule => (rule.Matches(model), rule.Evaluate(model).Satisfied),
            _ => throw new InvalidOperationException($"unknown rule '{name}'")
        };

    [Theory]
    [MemberData(nameof(EveryFlavourAndOutcome))]
    public async Task Should_agree_with_evaluate(string name, int model)
    {
        // Arrange
        var rules = NewRules();

        // Act
        var (matched, satisfied) = await MatchAndEvaluate(rules, name, model);

        // Assert — the invariant that makes a sampled explanation a faithful account of a fast-path decision
        matched.ShouldBe(satisfied);
        matched.ShouldBe(model > 0);
    }

    [Fact]
    public async Task Should_match_the_published_version_rather_than_the_default()
    {
        // Arrange
        var rules = NewRules();
        await rules.UpdateAsync(
            "number", """{ "rule": { "not": { "spec": "positive" } } }""", 1, new RuleChangeProvenance("test"));
        await rules.UpdateAsync(
            "number-async", """{ "rule": { "not": { "spec": "positive-async" } } }""", 1, new RuleChangeProvenance("test"));

        // Act
        var matched = ((NumberRule)rules.Find("number")!).Matches(1);
        var matchedAsync = await ((NumberAsyncRule)rules.Find("number-async")!).MatchesAsync(1);

        // Assert — a hot swap reaches the fast path too, not only the rich one
        matched.ShouldBeFalse();
        matchedAsync.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_match_against_the_pinned_world_when_a_snapshot_is_open()
    {
        // Arrange
        var rules = NewRules();
        var rule = (NumberRule)rules.Find("number")!;

        // Act — a publish lands inside a pinned decision
        using var snapshot = rules.PinSnapshot();
        await rules.UpdateAsync(
            "number", """{ "rule": { "not": { "spec": "positive" } } }""", 1, new RuleChangeProvenance("test"));
        var matched = rule.Matches(1);

        // Assert — one decision, one world, whichever entry point asks
        matched.ShouldBeTrue();
    }

    [Fact]
    public void Should_throw_when_the_rule_is_unbound()
    {
        // Arrange
        var rule = new NumberRule();

        // Act
        Action act = () => rule.Matches(1);

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldContain("has not been bound");
    }

    [Fact]
    public void Should_throw_synchronously_when_an_async_rule_is_unbound()
    {
        // Arrange
        var rule = new NumberAsyncRule();

        // Act — not awaited: the throw must happen at the call, not in a task nobody observes
        Action act = () => { _ = rule.MatchesAsync(1); };

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldContain("has not been bound");
    }

    [Fact]
    public void Should_reach_the_short_circuiting_fast_path_of_the_bound_spec()
    {
        // Arrange — a higher-order spec whose Matches stops at the first counterexample
        var calls = 0;
        var allPositive = Spec
            .Build((int n) => { calls++; return n > 0; })
            .AsAllSatisfied()
            .Create("all positive");
        var rule = new AllPositiveRule(allPositive);
        new RuleSet(new SpecRegistry().Register("all-positive", allPositive)).Add(rule);

        // Act
        var matched = rule.Matches([-1, 2, 3]);

        // Assert — Evaluate would have visited all three; Matches may stop at the first
        matched.ShouldBeFalse();
        calls.ShouldBe(1);
    }

    private sealed class AllPositiveRule(SpecBase<IEnumerable<int>, string> spec)
        : Rule<IEnumerable<int>, string>("all-positive", spec);
}
