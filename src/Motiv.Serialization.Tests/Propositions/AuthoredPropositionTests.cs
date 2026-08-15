using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class AuthoredPropositionTests
{
    [Fact]
    public void Should_produce_a_replacement_rather_than_mutate_when_rebound()
    {
        // Arrange
        var registry = new SpecRegistry();
        var propositions = new PropositionSet(registry, new InMemoryPropositionStore());
        var original = new AuthoredProposition(
            propositions, "customer.is-adult", "customer", """{"spec":"a"}""", 3, null,
            bound: null, quarantine: [], references: ["a"]);

        // Act
        var repaired = original.WithQuarantine([new RuleError("$", RuleErrorCode.InvalidNode, "broken")]);

        // Assert — the generation that holds the original must not see the change
        original.Quarantine.ShouldBeEmpty();
        repaired.Quarantine.Count.ShouldBe(1);
        repaired.Version.ShouldBe(original.Version);
        repaired.References.ShouldBe(original.References);
    }
}
