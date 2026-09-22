using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class DecisionSnapshotTests
{
    private static SpecBase<int, string> Positive { get; } = Spec.Build((int n) => n > 0).Create("positive");

    private sealed class LeftRule() : Rule<int, string>("left", Positive);
    private sealed class RightRule() : Rule<int, string>("right", Positive);

    private static RuleSet TwoRules()
    {
        var registry = new SpecRegistry().Register("positive", Positive);
        var rules = new RuleSet(registry);
        rules.Add(new LeftRule());
        rules.Add(new RightRule());
        return rules;
    }

    [Fact]
    public async Task Should_hold_one_world_across_several_evaluations()
    {
        // Arrange
        var rules = TwoRules();
        var left = (LeftRule)rules.Find("left")!;
        var right = (RightRule)rules.Find("right")!;

        // Act — a publish lands between the two evaluations of a single pinned decision
        using var snapshot = rules.PinSnapshot();
        var before = left.Evaluate(1).Satisfied;
        await rules.UpdateAsync(
            "right", """{ "rule": { "not": { "spec": "positive" } } }""", 1, new RuleChangeProvenance("test"));
        var after = right.Evaluate(1).Satisfied;

        // Assert — the decision sees the world it opened with, not a mix of two.
        // This is the whole point: a mix is a combination that was never published.
        before.ShouldBeTrue();
        after.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_see_the_new_world_once_the_pin_is_released()
    {
        // Arrange
        var rules = TwoRules();
        var right = (RightRule)rules.Find("right")!;
        using (rules.PinSnapshot())
        {
            await rules.UpdateAsync(
                "right", """{ "rule": { "not": { "spec": "positive" } } }""", 1, new RuleChangeProvenance("test"));
        }

        // Act & Assert — a pin is a decision, not a subscription
        right.Evaluate(1).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_let_the_outer_pin_own_the_lifetime_when_pins_nest()
    {
        // Arrange
        var rules = TwoRules();
        var right = (RightRule)rules.Find("right")!;

        // Act
        using var outer = rules.PinSnapshot();
        using (rules.PinSnapshot())
        {
            await rules.UpdateAsync(
                "right", """{ "rule": { "not": { "spec": "positive" } } }""", 1, new RuleChangeProvenance("test"));
        }

        // Assert — disposing the inner pin must not end the decision the outer one opened
        right.Evaluate(1).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_report_the_generation_it_pinned()
    {
        // Arrange
        var rules = TwoRules();

        // Act
        using var snapshot = rules.PinSnapshot();

        // Assert — what the response header stamps
        snapshot.Generation.ShouldBe(rules.Scope.Current.Sequence);
    }
}
