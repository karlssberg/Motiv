namespace Motiv.Serialization.Tests.Rules;

public class RulePublishAtomicityTests
{
    private sealed class NumberRule() : Rule<int, string>("number", Spec.Build((int n) => n > 0).Create("positive"));

    [Fact]
    public async Task Should_publish_a_rule_update_in_one_generation()
    {
        // Arrange
        var registry = new SpecRegistry();
        registry.Register("positive", Spec.Build((int n) => n > 0).Create("positive"));
        var rules = new RuleSet(registry);
        rules.Add(new NumberRule());
        var before = rules.Scope.WriteStamp;

        // Act
        var result = await rules.UpdateAsync(
            "number", """{ "rule": { "not": { "spec": "positive" } } }""", 1, new RuleChangeProvenance("test"));

        // Assert — the publication and the graph re-tracking are one world, not two
        result.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        (rules.Scope.WriteStamp - before).ShouldBe(1);
    }
}
