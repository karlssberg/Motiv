using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class PropositionPublishAtomicityTests
{
    [Fact]
    public async Task Should_publish_a_proposition_and_its_dependents_in_one_generation()
    {
        // Arrange — base, plus a proposition that references it, so a publish rebinds two nodes
        var registry = new SpecRegistry()
            .Register("positive", Spec.Build((int n) => n > 0).Create("positive"));
        var scope = new BindingScope(registry);
        var propositions = new PropositionSet(scope, new InMemoryPropositionStore());
        propositions.AddModel<int>("number");
        propositions.Load();

        (await propositions.CreateAsync("base", "number", """{ "rule": { "spec": "positive" } }""", null))
            .Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        (await propositions.CreateAsync("derived", "number", """{ "rule": { "spec": "base" } }""", null))
            .Outcome.ShouldBe(PropositionUpdateOutcome.Created);

        var before = scope.WriteStamp;

        // Act — republishing base rebinds derived as well
        var result = await propositions.UpdateAsync(
            "base", """{ "rule": { "not": { "spec": "positive" } } }""", 1);

        // Assert — one publish is one swap, however many nodes it rebound. Two swaps would let a
        // reader in between observe a combination that was never published.
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Updated);
        (scope.WriteStamp - before).ShouldBe(1);
    }
}
