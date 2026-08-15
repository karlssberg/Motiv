using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Rules;

public class RuleSlotTests
{
    private sealed class FirstRule() : Rule<int, string>("first", Spec.Build((int n) => n > 0).Create("positive"));
    private sealed class SecondRule() : Rule<int, string>("second", Spec.Build((int n) => n > 1).Create("big"));

    [Fact]
    public void Should_give_each_rule_a_distinct_stable_slot()
    {
        // Arrange
        var rules = new RuleSet(new SpecRegistry());
        var first = new FirstRule();
        var second = new SecondRule();

        // Act
        rules.Add(first);
        rules.Add(second);

        // Assert — a slot is permanent, so a later Add must not renumber an earlier rule
        first.Slot.ShouldBe(0);
        second.Slot.ShouldBe(1);
        rules.Scope.Current.RuleSlots.Length.ShouldBe(2);
    }

    [Fact]
    public void Should_refuse_to_evaluate_a_rule_that_was_never_added()
    {
        // Arrange
        var rule = new FirstRule();

        // Act & Assert — the message is load-bearing: it is what a developer sees on the mistake
        var exception = Should.Throw<InvalidOperationException>(() => rule.Evaluate(1));
        exception.Message.ShouldContain("has not been bound");
    }

    [Fact]
    public async Task Should_evaluate_through_the_generation_rather_than_the_rule()
    {
        // Arrange
        var registry = new SpecRegistry().Register("positive", Spec.Build((int n) => n > 0).Create("positive"));
        var rules = new RuleSet(registry);
        var rule = new FirstRule();
        rules.Add(rule);

        // Act
        await rules.UpdateAsync(
            "first", """{ "rule": { "not": { "spec": "positive" } } }""", expectedVersion: 1,
            new RuleChangeProvenance("test"));

        // Assert — the rule holds no state of its own; the swap alone changed what it evaluates
        rule.Evaluate(1).Satisfied.ShouldBeFalse();
        rule.Evaluate(-1).Satisfied.ShouldBeTrue();
    }

    /// <summary>
    /// Evaluation is pinned; administration is live. Both halves in one test, because the split is
    /// drawn member by member and has been drawn the wrong way before: a rule evaluated inside an open
    /// pin must keep seeing the world the pin froze, while the same rule's <c>Version</c> and
    /// <c>DocumentJson</c> must report the publish that has since landed.
    /// </summary>
    [Fact]
    public async Task Should_evaluate_the_pinned_world_while_reporting_the_live_one()
    {
        // Arrange
        var registry = new SpecRegistry().Register("positive", Spec.Build((int n) => n > 0).Create("positive"));
        var rules = new RuleSet(registry);
        var rule = new FirstRule();
        rules.Add(rule);

        using var pin = rules.Scope.Pin();

        // Act — a publish lands while this flow is pinned
        await rules.UpdateAsync(
            "first", """{ "rule": { "not": { "spec": "positive" } } }""", expectedVersion: 1,
            new RuleChangeProvenance("test"));

        // Assert — the decision still resolves against the world it pinned
        rule.Evaluate(1).Satisfied.ShouldBeTrue();

        // ...while the administrative reads see the publish, or a repair would be addressed against a
        // version the store no longer holds.
        rule.Version.ShouldBe(2);
        rule.DocumentJson.ShouldNotBeNull();
    }
}
