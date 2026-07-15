using System.Text.Json;
using Json.Schema;

namespace Motiv.Serialization.Tests;

public class RuleSchemaTests
{
    private static readonly JsonSchema Schema = JsonSchema.FromFile(
        Path.Combine(AppContext.BaseDirectory, "schemas", "rule.v1.json"));

    private static bool IsValid(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Schema.Evaluate(document.RootElement).IsValid;
    }

    public static TheoryData<string> ValidDocuments => new()
    {
        """{ "rule": { "spec": "is-positive" } }""",
        """{ "$schema": "https://example.com/rule.v1.json", "name": "doc", "rule": { "spec": "a" } }""",
        """{ "rule": { "and": [ { "spec": "a" }, { "not": { "spec": "b" } } ] } }""",
        """{ "rule": { "or": [ { "spec": "a" }, { "spec": "b" }, { "spec": "c" } ] } }""",
        """{ "rule": { "xor": [ { "spec": "a" }, { "spec": "b" } ] } }""",
        """{ "rule": { "andAlso": [ { "spec": "a" }, { "spec": "b" } ] } }""",
        """{ "rule": { "orElse": [ { "spec": "a" }, { "spec": "b" } ] } }""",
        """{ "rule": { "spec": "a", "whenTrue": "yes", "whenFalse": "no", "name": "n" } }""",
        """{ "rule": { "spec": "a", "whenTrue": { "code": 1 }, "whenFalse": { "code": 2 } } }""",
        """{ "rule": { "expression": "Age >= @minAge" } }""",
        """{ "parameters": { "minAge": { "type": "integer", "default": 18 } }, "rule": { "spec": "a" } }""",
        """{ "rule": { "asAllSatisfied": { "spec": "a" }, "path": "Orders" } }""",
        """{ "rule": { "asAnySatisfied": { "spec": "a" } } }""",
        """{ "rule": { "asNSatisfied": { "spec": "a" }, "n": 3 } }""",
        """{ "rule": { "asAtLeastNSatisfied": { "spec": "a" }, "n": "@minOrders", "path": "Account.Orders" } }""",
        """{ "rule": { "asAtMostNSatisfied": { "spec": "a" }, "n": 2 } }"""
    };

    [Theory]
    [MemberData(nameof(ValidDocuments))]
    public void Should_accept_valid_documents(string json)
    {
        IsValid(json).ShouldBeTrue();
    }

    public static TheoryData<string> InvalidDocuments => new()
    {
        """{ }""",
        """{ "rule": { } }""",
        """{ "frobnicate": 1, "rule": { "spec": "a" } }""",
        """{ "rule": { "spec": "a", "frobnicate": true } }""",
        """{ "rule": { "spec": "" } }""",
        """{ "rule": { "spec": "a", "expression": "Age >= 18" } }""",
        """{ "rule": { "and": [ { "spec": "a" } ] } }""",
        """{ "rule": { "not": [ { "spec": "a" } ] } }""",
        """{ "rule": { "spec": "a", "whenTrue": "yes" } }""",
        """{ "rule": { "spec": "a", "whenTrue": "yes", "whenFalse": { "code": 2 } } }""",
        """{ "rule": { "spec": "a", "whenTrue": 1, "whenFalse": 2 } }""",
        """{ "parameters": { "minAge": { "default": 18 } }, "rule": { "spec": "a" } }""",
        """{ "parameters": { "minAge": { "type": "decimal" } }, "rule": { "spec": "a" } }""",
        """{ "rule": { "asNSatisfied": { "spec": "a" } } }""",
        """{ "rule": { "asAllSatisfied": { "spec": "a" }, "n": 3 } }""",
        """{ "rule": { "asAllSatisfied": { "spec": "a" }, "path": "Orders..Items" } }""",
        """{ "rule": { "asNSatisfied": { "spec": "a" }, "n": "minOrders" } }"""
    };

    [Theory]
    [MemberData(nameof(InvalidDocuments))]
    public void Should_reject_invalid_documents(string json)
    {
        IsValid(json).ShouldBeFalse();
    }
}
