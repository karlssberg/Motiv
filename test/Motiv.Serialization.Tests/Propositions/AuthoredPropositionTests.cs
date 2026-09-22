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

    [Fact]
    public void Should_carry_the_version_across_and_clear_quarantine_when_bound()
    {
        // Arrange — a quarantined proposition, about to be repaired by a fresh binding
        var registry = new SpecRegistry();
        var propositions = new PropositionSet(registry, new InMemoryPropositionStore());
        var original = new AuthoredProposition(
            propositions, "customer.is-adult", "customer", """{"spec":"a"}""", 3, null,
            bound: null, quarantine: [new RuleError("$", RuleErrorCode.InvalidNode, "broken")],
            references: ["a"]);
        var entry = new SpecRegistryEntry("customer.is-adult", typeof(int), typeof(string), false, new object());

        // Act
        var rebound = original.WithBinding(entry);

        // Assert — the document did not change, only what it resolves to, so bumping the version
        // would spuriously conflict with an editor's open draft; binding again is what resolves a
        // quarantine
        rebound.Version.ShouldBe(original.Version);
        rebound.References.ShouldBe(original.References);
        rebound.Bound.ShouldBeSameAs(entry);
        rebound.Quarantine.ShouldBeEmpty();
    }
}
