using System.Text.Json;

namespace Motiv.Serialization.AspNetCore.Tests;

/// <summary>
/// What a reproduction looks like over the wire: a reference capture is its key and nothing else,
/// a whole capture is the host's JSON of the model, and the fidelity verdict travels as names.
/// </summary>
public class ReproductionContractsTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static DecisionRecord Record(DecisionInput input) =>
        new(Guid.NewGuid(), "corr-1", DateTimeOffset.UtcNow, "alice", "can-checkout", 2, "build-1",
            [new PropositionVersion("customer.eligible", 1)], input,
            new RuleEvaluationResult<object?>(true, "r", ["r"], [], "r", new ExplanationNode(["r"], [])));

    [Fact]
    public void Should_project_a_reference_capture_as_its_key()
    {
        var reproduction = new Reproduction(
            Record(DecisionInput.Reference("cust-42")), Rule: null, Propositions: [],
            new ReproducedModel(ReproducedModelKind.Reference, null, "cust-42"), Replayed: null,
            new ReproductionFidelity([new FidelityNote(FidelityReason.ModelUnresolved, "no resolver")]),
            CSharp: null, CSharpWarnings: []);

        var entry = DecisionsContracts.Entry(reproduction, Json);

        entry.Decision.Input!.Kind.ShouldBe("Reference");
        entry.Decision.Input.Value.ShouldBeNull();
        entry.Decision.Input.Key!.ShouldBe("cust-42");
        entry.Model.Kind.ShouldBe("Reference");
        entry.Model.Value.ShouldBeNull();
        entry.Model.Key!.ShouldBe("cust-42");
        entry.Fidelity.IsExact.ShouldBeFalse();
        entry.Fidelity.Notes.ShouldHaveSingleItem().Reason.ShouldBe("ModelUnresolved");
        entry.Rule.ShouldBeNull();
        entry.CSharp.ShouldBeNull();
    }

    [Fact]
    public void Should_project_a_whole_capture_as_the_hosts_json()
    {
        var model = new { age = 30, isActive = true };
        var reproduction = new Reproduction(
            Record(DecisionInput.Whole(model)),
            new StoredRuleVersion("can-checkout", 2, """{ "rule": { "spec": "x" } }""", "alice", DateTimeOffset.UtcNow, null, null, "build-1"),
            [new StoredPropositionVersion("customer.eligible", 1, "customer", """{ "rule": { "spec": "y" } }""", null, "alice", DateTimeOffset.UtcNow, null, null, null)],
            new ReproducedModel(ReproducedModelKind.Whole, model, null),
            new RuleEvaluationResult<object?>(true, "r", ["r"], [], "r", new ExplanationNode(["r"], [])),
            new ReproductionFidelity([]), "// printed", ["a warning"]);

        var entry = DecisionsContracts.Entry(reproduction, Json);

        entry.Decision.Input!.Kind.ShouldBe("Whole");
        entry.Decision.Input.Value!.Value.GetProperty("age").GetInt32().ShouldBe(30);
        entry.Decision.Input.Key.ShouldBeNull();
        entry.Model.Value!.Value.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        entry.Rule!.Document!.Value.GetProperty("rule").GetProperty("spec").GetString()!.ShouldBe("x");
        entry.Propositions.ShouldHaveSingleItem().Document!.Value.GetProperty("rule").GetProperty("spec").GetString()!.ShouldBe("y");
        entry.Fidelity.IsExact.ShouldBeTrue();
        entry.Replayed!.Satisfied.ShouldBeTrue();
        entry.CSharp!.ShouldBe("// printed");
        entry.CSharpWarnings.ShouldBe(new[] { "a warning" });
    }
}
