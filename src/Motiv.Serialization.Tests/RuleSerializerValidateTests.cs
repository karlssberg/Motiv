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
    [InlineData("""{ "rule": { "spec": "a", "whenTrue": { "code": 1 }, "whenFalse": { "code": 2 }, "name": "coded" } }""")]
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

    [Theory]
    [InlineData("""{ "rule": { "asAllSatisfied": { "spec": "a" }, "path": "orders", "name": "all" } }""")]
    [InlineData("""{ "rule": { "asAnySatisfied": { "spec": "a" }, "path": "orders", "whenTrue": "some", "whenFalse": "none" } }""")]
    [InlineData("""{ "rule": { "asNSatisfied": { "spec": "a" }, "n": 3, "path": "orders", "name": "exactly three" } }""")]
    [InlineData("""{ "rule": { "asAtLeastNSatisfied": { "spec": "a" }, "n": "@minOrders", "path": "orders", "name": "quota" } }""")]
    [InlineData("""{ "rule": { "asAtMostNSatisfied": { "spec": "a" }, "n": 0, "path": "Orders", "name": "none" } }""")]
    public void Should_accept_well_formed_higher_order_nodes(string json)
    {
        // Act
        var errors = Validate(json);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Should_require_n_on_counted_higher_order_nodes()
    {
        // Act
        var errors = Validate("""{ "rule": { "asNSatisfied": { "spec": "a" }, "path": "orders", "name": "x" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule");
        error.Message.ShouldContain("'n'");
    }

    [Fact]
    public void Should_reject_n_on_uncounted_higher_order_nodes()
    {
        // Act
        var errors = Validate("""{ "rule": { "asAllSatisfied": { "spec": "a" }, "n": 3, "path": "orders", "name": "x" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule.n");
    }

    [Theory]
    [InlineData("""{ "rule": { "asNSatisfied": { "spec": "a" }, "n": -1, "path": "orders", "name": "x" } }""")]
    [InlineData("""{ "rule": { "asNSatisfied": { "spec": "a" }, "n": 2.5, "path": "orders", "name": "x" } }""")]
    [InlineData("""{ "rule": { "asNSatisfied": { "spec": "a" }, "n": "minOrders", "path": "orders", "name": "x" } }""")]
    [InlineData("""{ "rule": { "asNSatisfied": { "spec": "a" }, "n": "@1bad", "path": "orders", "name": "x" } }""")]
    public void Should_reject_malformed_n_values(string json)
    {
        // Act
        var errors = Validate(json);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule.n");
    }

    [Theory]
    [InlineData("""{ "rule": { "spec": "a", "n": 3 } }""", "$.rule.n")]
    [InlineData("""{ "rule": { "spec": "a", "path": "Orders" } }""", "$.rule.path")]
    public void Should_reject_higher_order_properties_on_other_nodes(string json, string path)
    {
        // Act
        var errors = Validate(json);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe(path);
    }

    [Fact]
    public void Should_reject_an_empty_higher_order_path()
    {
        // Act
        var errors = Validate("""{ "rule": { "asAllSatisfied": { "spec": "a" }, "path": "", "name": "x" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule.path");
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
    [InlineData("""{ "parameters": { "minAge": { "type": "decimal" } }, "rule": { "spec": "a" } }""", "$.parameters.minAge")]
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

    [Theory]
    [InlineData("""{ "rule": { "asAllSatisfied": { "spec": "a" }, "path": "xs" } }""")]
    [InlineData("""{ "rule": { "asAnySatisfied": { "spec": "a" }, "path": "xs" } }""")]
    [InlineData("""{ "rule": { "asNSatisfied": { "spec": "a" }, "n": 2, "path": "xs" } }""")]
    [InlineData("""{ "rule": { "asAtLeastNSatisfied": { "spec": "a" }, "n": 1, "path": "xs" } }""")]
    [InlineData("""{ "rule": { "asAtMostNSatisfied": { "spec": "a" }, "n": 3, "path": "xs" } }""")]
    [InlineData("""{ "rule": { "asAllSatisfied": { "and": [ { "spec": "a" }, { "spec": "b" } ] }, "path": "xs" } }""")]
    public void Should_accept_valid_higher_order_nodes(string json) =>
        Validate(json).ShouldBeEmpty();

    [Fact]
    public void Should_reject_a_count_on_a_quantifier_that_takes_none()
    {
        var errors = Validate("""{ "rule": { "asAllSatisfied": { "spec": "a" }, "n": 2, "path": "xs" } }""");
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule.n");
        error.Message.ShouldContain("'n'");
    }

    [Fact]
    public void Should_reject_a_count_on_a_quantifier_that_takes_none_even_when_it_is_a_string()
    {
        var errors = Validate("""{ "rule": { "asAllSatisfied": { "spec": "a" }, "n": "@x", "path": "xs" } }""");
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule.n");
        error.Message.ShouldContain("only valid on");
    }

    [Fact]
    public void Should_reject_a_missing_count_on_an_n_quantifier()
    {
        var errors = Validate("""{ "rule": { "asNSatisfied": { "spec": "a" }, "path": "xs" } }""");
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Message.ShouldContain("'n'");
    }

    [Fact]
    public void Should_reject_a_missing_path()
    {
        var errors = Validate("""{ "rule": { "asAllSatisfied": { "spec": "a" } } }""");
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Message.ShouldContain("'path'");
    }

    [Fact]
    public void Should_reject_an_empty_path()
    {
        var errors = Validate("""{ "rule": { "asAllSatisfied": { "spec": "a" }, "path": " " } }""");
        errors.ShouldContain(e => e.Code == RuleErrorCode.InvalidNode && e.Path == "$.rule.path");
    }

    [Fact]
    public void Should_accept_a_parameter_reference_as_a_higher_order_count()
    {
        // '@param' counts are resolved later by RuleParameterSubstituter; structurally valid here.
        var errors = Validate("""{ "rule": { "asNSatisfied": { "spec": "a" }, "n": "@minLarge", "path": "xs" } }""");
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Should_accept_documents_deeper_than_the_default_json_reader_limit()
    {
        // Arrange: 35 nested "and" levels is legal under the default MaxDocumentDepth of 64,
        // but costs ~70 JSON levels, which exceeds System.Text.Json's default reader depth of 64.
        var json = """{ "rule": """;
        for (var i = 0; i < 35; i++)
            json += """{ "and": [ { "spec": "a" }, """;
        json += """{ "spec": "a" }""";
        for (var i = 0; i < 35; i++)
            json += "] }";
        json += "}";

        // Act
        var errors = Validate(json);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Should_report_a_non_string_schema_reference()
    {
        // Act
        var errors = Validate("""{ "$schema": 1, "rule": { "spec": "a" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.$schema");
        error.Message.ShouldContain("string");
    }

    [Fact]
    public void Should_require_a_name_on_nodes_with_object_payloads()
    {
        // Act
        var errors = Validate(
            """{ "rule": { "spec": "a", "whenTrue": { "code": 1 }, "whenFalse": { "code": 2 } } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule");
        error.Message.ShouldContain("name");
    }

    [Fact]
    public void Should_accept_named_nodes_with_object_payloads()
    {
        // Act
        var errors = Validate(
            """{ "rule": { "spec": "a", "whenTrue": { "code": 1 }, "whenFalse": { "code": 2 }, "name": "coded" } }""");

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Should_reject_an_empty_expression_string()
    {
        // Act
        var errors = Validate("""{ "rule": { "expression": "" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule.expression");
    }

    [Fact]
    public void Should_propagate_a_structurally_invalid_child_through_a_higher_order_node()
    {
        // Act
        var errors = Validate("""{ "rule": { "asAllSatisfied": { "frobnicate": true } } }""");

        // Assert
        errors.ShouldContain(error =>
            error.Code == RuleErrorCode.InvalidNode && error.Path == "$.rule.asAllSatisfied.frobnicate");
    }

    [Fact]
    public void Should_reject_whenFalse_without_whenTrue()
    {
        // Act
        var errors = Validate("""{ "rule": { "spec": "a", "whenFalse": "no" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule");
        error.Message.ShouldContain("together");
    }

    [Fact]
    public void Should_reject_a_parameter_declaration_that_is_not_an_object()
    {
        // Act
        var errors = Validate("""{ "parameters": { "p": "oops" }, "rule": { "spec": "a" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.parameters.p");
    }

    [Fact]
    public void Should_reject_a_non_string_parameter_type()
    {
        // Act
        var errors = Validate("""{ "parameters": { "p": { "type": 123 } }, "rule": { "spec": "a" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.parameters.p");
    }

    [Fact]
    public void Should_reject_a_bare_at_sign_as_an_n_parameter_reference()
    {
        // Act
        var errors = Validate("""{ "rule": { "asNSatisfied": { "spec": "a" }, "n": "@", "name": "x" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule.n");
    }

    [Fact]
    public void Should_accept_a_parameter_reference_with_underscores_and_digits()
    {
        // Act
        var errors = Validate(
            """
            {
              "parameters": { "Min_Age2": { "type": "integer", "default": 1 } },
              "rule": { "asNSatisfied": { "spec": "a" }, "n": "@Min_Age2", "name": "x" }
            }
            """);

        // Assert
        errors.ShouldBeEmpty();
    }
}
