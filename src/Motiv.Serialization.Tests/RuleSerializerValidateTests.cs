using Motiv.Serialization;

namespace Motiv.Serialization.Tests;

public class RuleSerializerValidateTests
{
    private static IReadOnlyList<RuleError> Validate(string json, RuleSerializerOptions? options = null) =>
        new RuleSerializer(new SpecRegistry(), options).Validate(json);

    [Theory]
    [InlineData("""{ "rule": { "spec": "is-positive" } }""")]
    [InlineData("""{ "$schema": "https://example.com/rule.v1.json", "name": "doc", "rule": { "spec": "a" } }""")]
    [InlineData("""{ "rule": { "and": [ { "spec": "a" }, { "not": { "spec": "b" } } ] } }""")]
    [InlineData("""{ "rule": { "spec": "a", "whenTrue": "yes", "whenFalse": "no", "name": "n" } }""")]
    [InlineData("""{ "rule": { "expression": "Age >= 18" } }""")]
    [InlineData("""{ "rule": { "spec": "a", "whenTrue": { "code": 1 }, "whenFalse": { "code": 2 } } }""")]
    [InlineData("""{ "parameters": { "minAge": { "type": "integer", "default": 18 } }, "rule": { "spec": "a" } }""")]
    public void Should_report_no_errors_for_structurally_valid_documents(string json)
    {
        // Act
        var errors = Validate(json);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("not json at all", "$")]
    [InlineData("[]", "$")]
    [InlineData("\"rule\"", "$")]
    public void Should_reject_documents_that_are_not_json_objects(string json, string expectedPath)
    {
        // Act
        var errors = Validate(json);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe(expectedPath);
    }

    [Fact]
    public void Should_reject_a_document_without_a_rule()
    {
        // Act
        var errors = Validate("""{ "name": "doc" }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$");
        error.Message.ShouldContain("rule");
    }

    [Fact]
    public void Should_reject_an_unknown_envelope_property()
    {
        // Act
        var errors = Validate("""{ "frobnicate": 1, "rule": { "spec": "a" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.frobnicate");
    }

    [Fact]
    public void Should_reject_a_node_with_no_operator()
    {
        // Act
        var errors = Validate("""{ "rule": { "name": "empty" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule");
        error.Message.ShouldContain("exactly one");
    }

    [Fact]
    public void Should_reject_a_node_with_two_operators()
    {
        // Act
        var errors = Validate("""{ "rule": { "spec": "a", "expression": "Age >= 18" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule");
    }

    [Fact]
    public void Should_reject_an_unknown_node_property()
    {
        // Act
        var errors = Validate("""{ "rule": { "spec": "a", "frobnicate": true } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule.frobnicate");
        error.Message.ShouldContain("unknown property");
    }

    [Fact]
    public void Should_explain_that_higher_order_properties_are_not_yet_supported()
    {
        // Act
        var errors = Validate("""{ "rule": { "asAllSatisfied": { "spec": "a" } } }""");

        // Assert
        errors.ShouldContain(error =>
            error.Code == RuleErrorCode.InvalidNode &&
            error.Path == "$.rule.asAllSatisfied" &&
            error.Message.Contains("not yet supported"));
    }

    [Theory]
    [InlineData("""{ "rule": { "and": { "spec": "a" } } }""")]
    [InlineData("""{ "rule": { "and": [ { "spec": "a" } ] } }""")]
    [InlineData("""{ "rule": { "or": [] } }""")]
    public void Should_reject_binary_operators_without_at_least_two_operands(string json)
    {
        // Act
        var errors = Validate(json);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Message.ShouldContain("at least two");
    }

    [Fact]
    public void Should_reject_a_not_operator_that_is_not_an_object()
    {
        // Act
        var errors = Validate("""{ "rule": { "not": [ { "spec": "a" } ] } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule.not");
    }

    [Fact]
    public void Should_reject_whenTrue_without_whenFalse()
    {
        // Act
        var errors = Validate("""{ "rule": { "spec": "a", "whenTrue": "yes" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule");
        error.Message.ShouldContain("together");
    }

    [Fact]
    public void Should_reject_mixed_payload_kinds()
    {
        // Act
        var errors = Validate("""{ "rule": { "spec": "a", "whenTrue": "yes", "whenFalse": { "code": 2 } } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.MixedWhenTrueFalseKinds);
        error.Path.ShouldBe("$.rule");
    }

    [Theory]
    [InlineData("""{ "rule": { "spec": "a", "whenTrue": 1, "whenFalse": 2 } }""")]
    [InlineData("""{ "rule": { "spec": "a", "whenTrue": true, "whenFalse": false } }""")]
    [InlineData("""{ "rule": { "spec": "a", "whenTrue": ["yes"], "whenFalse": ["no"] } }""")]
    public void Should_reject_payloads_that_are_neither_strings_nor_objects(string json)
    {
        // Act
        var errors = Validate(json);

        // Assert
        errors.ShouldAllBe(error => error.Code == RuleErrorCode.InvalidNode);
        errors.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("""{ "rule": { "spec": "" } }""", "$.rule.spec")]
    [InlineData("""{ "rule": { "spec": "a", "whenTrue": " ", "whenFalse": "no" } }""", "$.rule.whenTrue")]
    [InlineData("""{ "rule": { "spec": "a", "name": "" } }""", "$.rule.name")]
    [InlineData("""{ "name": " ", "rule": { "spec": "a" } }""", "$.name")]
    public void Should_reject_blank_strings(string json, string expectedPath)
    {
        // Act
        var errors = Validate(json);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe(expectedPath);
    }

    [Theory]
    [InlineData("""{ "parameters": [ "minAge" ], "rule": { "spec": "a" } }""", "$.parameters")]
    [InlineData("""{ "parameters": { "minAge": { "default": 18 } }, "rule": { "spec": "a" } }""", "$.parameters.minAge")]
    [InlineData("""{ "parameters": { "minAge": { "type": "decimal" } }, "rule": { "spec": "a" } }""", "$.parameters.minAge.type")]
    [InlineData("""{ "parameters": { "minAge": { "type": "integer", "frobnicate": 1 } }, "rule": { "spec": "a" } }""", "$.parameters.minAge.frobnicate")]
    public void Should_validate_parameter_declarations(string json, string expectedPath)
    {
        // Act
        var errors = Validate(json);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe(expectedPath);
    }

    [Fact]
    public void Should_reject_a_document_that_exceeds_the_maximum_depth()
    {
        // Arrange
        var options = new RuleSerializerOptions { MaxDocumentDepth = 3 };
        const string json =
            """{ "rule": { "not": { "not": { "not": { "spec": "a" } } } } }""";

        // Act
        var errors = Validate(json, options);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.DocumentTooLarge);
    }

    [Fact]
    public void Should_reject_a_document_that_exceeds_the_maximum_node_count()
    {
        // Arrange
        var options = new RuleSerializerOptions { MaxNodeCount = 2 };
        const string json =
            """{ "rule": { "and": [ { "spec": "a" }, { "spec": "b" }, { "spec": "c" } ] } }""";

        // Act
        var errors = Validate(json, options);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.DocumentTooLarge);
    }

    [Fact]
    public void Should_report_payload_errors_even_when_the_operator_subtree_fails()
    {
        // Act
        var errors = Validate("""{ "rule": { "and": [ { "spec": "a" }, { "spec": "" } ], "whenTrue": "x" } }""");

        // Assert
        errors.Count.ShouldBe(2);
        errors.ShouldContain(error => error.Path == "$.rule.and[1].spec");
        errors.ShouldContain(error => error.Path == "$.rule" && error.Message.Contains("together"));
    }

    [Fact]
    public void Should_report_payload_errors_on_a_node_with_no_operator()
    {
        // Act
        var errors = Validate("""{ "rule": { "whenTrue": "x" } }""");

        // Assert
        errors.Count.ShouldBe(2);
        errors.ShouldContain(error => error.Code == RuleErrorCode.InvalidNode && error.Message.Contains("exactly one"));
        errors.ShouldContain(error => error.Path == "$.rule" && error.Message.Contains("together"));
    }

    [Fact]
    public void Should_not_report_a_payload_error_when_valid_payloads_sit_on_a_failed_operator()
    {
        // Act: the node has no operator, but its whenTrue/whenFalse pair is well-formed
        var errors = Validate("""{ "rule": { "name": "x", "whenTrue": "yes", "whenFalse": "no" } }""");

        // Assert: only the missing-operator error is reported; the valid payloads add nothing
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Message.ShouldContain("exactly one");
    }

    [Fact]
    public void Should_report_multiple_errors_in_one_pass()
    {
        // Act
        var errors = Validate(
            """{ "frobnicate": 1, "rule": { "and": [ { "spec": "" }, { "name": "no operator" } ] } }""");

        // Assert
        errors.Count.ShouldBe(3);
    }
}
