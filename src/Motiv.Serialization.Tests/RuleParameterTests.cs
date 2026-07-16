namespace Motiv.Serialization.Tests;

using static SpecAssertions;

public class RuleParameterTests
{
    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0)
            .WhenTrue("is positive")
            .WhenFalse("is not positive")
            .Create();

    private static RuleSerializer CreateSerializer() =>
        new(new SpecRegistry().Register("is-positive", IsPositive));

    private static IReadOnlyList<RuleError> Validate(string json) =>
        new RuleSerializer(new SpecRegistry()).Validate(json);

    [Theory]
    [InlineData("""{ "parameters": { "p": { "type": "integer", "default": "x" } }, "rule": { "spec": "a" } }""")]
    [InlineData("""{ "parameters": { "p": { "type": "integer", "default": 2.5 } }, "rule": { "spec": "a" } }""")]
    [InlineData("""{ "parameters": { "p": { "type": "number", "default": true } }, "rule": { "spec": "a" } }""")]
    [InlineData("""{ "parameters": { "p": { "type": "string", "default": 1 } }, "rule": { "spec": "a" } }""")]
    [InlineData("""{ "parameters": { "p": { "type": "boolean", "default": "yes" } }, "rule": { "spec": "a" } }""")]
    public void Should_report_defaults_that_do_not_match_the_declared_type(string json)
    {
        // Act
        var errors = Validate(json);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.parameters.p.default");
    }

    [Fact]
    public void Should_report_an_integer_default_outside_32_bit_range()
    {
        // Act
        var errors = Validate(
            """{ "parameters": { "p": { "type": "integer", "default": 2147483648 } }, "rule": { "spec": "a" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.parameters.p.default");
    }

    [Theory]
    [InlineData("""{ "parameters": { "p": { "type": "integer", "default": 18 } }, "rule": { "spec": "a" } }""")]
    [InlineData("""{ "parameters": { "p": { "type": "number", "default": 1.5 } }, "rule": { "spec": "a" } }""")]
    [InlineData("""{ "parameters": { "p": { "type": "string", "default": "x" } }, "rule": { "spec": "a" } }""")]
    [InlineData("""{ "parameters": { "p": { "type": "boolean", "default": false } }, "rule": { "spec": "a" } }""")]
    public void Should_accept_defaults_that_match_the_declared_type(string json)
    {
        // Act
        var errors = Validate(json);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Should_throw_when_a_required_parameter_is_not_supplied()
    {
        // Arrange
        const string json =
            """{ "parameters": { "minOrders": { "type": "integer" } }, "rule": { "spec": "is-positive" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.MissingParameter);
        error.Path.ShouldBe("$.parameters.minOrders");
    }

    [Fact]
    public void Should_throw_when_a_surplus_parameter_is_supplied()
    {
        // Arrange
        const string json = """{ "rule": { "spec": "is-positive" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json, new { extra = 1 });

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.SurplusParameter);
        error.Path.ShouldBe("$.parameters.extra");
    }

    [Fact]
    public void Should_throw_when_a_supplied_parameter_does_not_match_the_declared_type()
    {
        // Arrange
        const string json =
            """{ "parameters": { "minOrders": { "type": "integer" } }, "rule": { "spec": "is-positive" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json, new { minOrders = "three" });

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.ParameterTypeMismatch);
        error.Path.ShouldBe("$.parameters.minOrders");
    }

    [Fact]
    public void Should_accept_required_parameters_from_an_anonymous_object()
    {
        // Arrange
        const string json =
            """{ "parameters": { "minOrders": { "type": "integer" } }, "rule": { "spec": "is-positive" } }""";

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json, new { minOrders = 3 });

        // Assert
        loaded.ShouldNotBeNull();
    }

    [Fact]
    public void Should_accept_required_parameters_from_a_dictionary()
    {
        // Arrange
        const string json =
            """{ "parameters": { "minOrders": { "type": "integer" } }, "rule": { "spec": "is-positive" } }""";
        var parameters = new Dictionary<string, object?> { ["minOrders"] = 3L };

        // Act — a long that fits in 32 bits coerces to an integer parameter
        var loaded = CreateSerializer().Deserialize<int>(json, parameters);

        // Assert
        loaded.ShouldNotBeNull();
    }

    [Fact]
    public void Should_interpolate_default_parameter_values_into_payload_strings()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": { "minAge": { "type": "integer", "default": 18 } },
              "rule": { "spec": "is-positive", "whenTrue": "at least {minAge}", "whenFalse": "under {minAge}" }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .WhenTrue("at least 18")
            .WhenFalse("under 18")
            .Create();

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_interpolate_supplied_values_over_defaults()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": { "minAge": { "type": "integer", "default": 18 } },
              "rule": { "spec": "is-positive", "whenTrue": "at least {minAge}", "whenFalse": "under {minAge}" }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .WhenTrue("at least 21")
            .WhenFalse("under 21")
            .Create();

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json, new { minAge = 21 });

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_format_number_and_boolean_parameters_invariantly()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": {
                "rate": { "type": "number", "default": 1.5 },
                "strict": { "type": "boolean", "default": true }
              },
              "rule": { "spec": "is-positive", "whenTrue": "rate {rate} strict {strict}", "whenFalse": "no" }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .WhenTrue("rate 1.5 strict true")
            .WhenFalse("no")
            .Create();

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_unescape_doubled_braces_as_literals()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": { "minAge": { "type": "integer", "default": 18 } },
              "rule": { "spec": "is-positive", "whenTrue": "{{age}} >= {minAge}", "whenFalse": "no" }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .WhenTrue("{age} >= 18")
            .WhenFalse("no")
            .Create();

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_report_an_unknown_parameter_reference_in_a_payload_string()
    {
        // Arrange
        const string json =
            """{ "rule": { "spec": "is-positive", "whenTrue": "at least {minAge}", "whenFalse": "no" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.UnknownParameterReference);
        error.Path.ShouldBe("$.rule.whenTrue");
    }

    [Theory]
    [InlineData("""{ "rule": { "spec": "is-positive", "whenTrue": "broken {", "whenFalse": "no" } }""")]
    [InlineData("""{ "rule": { "spec": "is-positive", "whenTrue": "broken }", "whenFalse": "no" } }""")]
    public void Should_report_unmatched_braces_in_a_payload_string(string json)
    {
        // Act
        var act = () => CreateSerializer().Deserialize<int>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule.whenTrue");
    }

    [Fact]
    public void Should_report_a_payload_that_interpolates_to_whitespace()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": { "label": { "type": "string", "default": " " } },
              "rule": { "spec": "is-positive", "whenTrue": "{label}", "whenFalse": "no" }
            }
            """;

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule.whenTrue");
    }

    private const string DuplicateParameterJson =
        """
        {
          "parameters": {
            "p": { "type": "integer", "default": 1 },
            "p": { "type": "integer", "default": 2 }
          },
          "rule": { "spec": "a" }
        }
        """;

    [Fact]
    public void Should_report_a_duplicate_parameter_declaration_structurally()
    {
        // Act
        var errors = Validate(DuplicateParameterJson);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.parameters.p");
        error.Message.ShouldContain("duplicate");
    }

    [Fact]
    public void Should_report_a_duplicate_parameter_declaration_semantically_without_throwing()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": {
                "p": { "type": "integer", "default": 1 },
                "p": { "type": "integer", "default": 2 }
              },
              "rule": { "spec": "is-positive" }
            }
            """;

        // Act
        var errors = CreateSerializer().Validate<int>(json);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.parameters.p");
        error.Message.ShouldContain("duplicate");
    }

    [Fact]
    public void Should_throw_a_rule_serialization_exception_for_a_duplicate_parameter_declaration()
    {
        // Act
        var act = () => CreateSerializer().Deserialize<int>(DuplicateParameterJson);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.parameters.p");
        error.Message.ShouldContain("duplicate");
    }

    [Fact]
    public void Should_not_crash_when_supplied_parameters_are_a_value_typed_dictionary()
    {
        // Arrange
        const string json =
            """{ "parameters": { "minOrders": { "type": "integer" } }, "rule": { "spec": "is-positive" } }""";

        // A Dictionary<string, int> does not match the IReadOnlyDictionary<string, object?> overload
        // (TValue is invariant), so it falls through to property reflection instead. It degrades to
        // ordinary Missing/Surplus parameter errors rather than crashing on the reflected Item indexer.
        var parameters = new Dictionary<string, int> { ["minOrders"] = 3 };

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json, parameters);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        exception.Errors.ShouldContain(error =>
            error.Code == RuleErrorCode.MissingParameter && error.Path == "$.parameters.minOrders");
    }

    [Fact]
    public void Should_treat_a_null_parameters_object_as_no_parameters()
    {
        // Arrange
        const string json = """{ "rule": { "spec": "is-positive" } }""";

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json, (object?)null);

        // Assert
        loaded.ShouldNotBeNull();
    }

    [Fact]
    public void Should_reject_a_supplied_long_integer_value_outside_32_bit_range()
    {
        // Arrange
        const string json = """{ "parameters": { "big": { "type": "integer" } }, "rule": { "spec": "is-positive" } }""";
        var parameters = new Dictionary<string, object?> { ["big"] = 5_000_000_000L };

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json, parameters);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.ParameterTypeMismatch);
        error.Path.ShouldBe("$.parameters.big");
    }

    [Fact]
    public void Should_coerce_a_supplied_double_value_for_a_number_parameter()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": { "rate": { "type": "number" } },
              "rule": { "spec": "is-positive", "whenTrue": "rate {rate}", "whenFalse": "no" }
            }
            """;
        var expected = Spec.Build(IsPositive).WhenTrue("rate 1.25").WhenFalse("no").Create();
        var parameters = new Dictionary<string, object?> { ["rate"] = 1.25d };

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json, parameters);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_coerce_a_supplied_float_value_for_a_number_parameter()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": { "rate": { "type": "number" } },
              "rule": { "spec": "is-positive", "whenTrue": "rate {rate}", "whenFalse": "no" }
            }
            """;
        var expected = Spec.Build(IsPositive).WhenTrue("rate 1.5").WhenFalse("no").Create();
        var parameters = new Dictionary<string, object?> { ["rate"] = 1.5f };

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json, parameters);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_coerce_a_supplied_int_value_for_a_number_parameter()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": { "rate": { "type": "number" } },
              "rule": { "spec": "is-positive", "whenTrue": "rate {rate}", "whenFalse": "no" }
            }
            """;
        var expected = Spec.Build(IsPositive).WhenTrue("rate 3").WhenFalse("no").Create();
        var parameters = new Dictionary<string, object?> { ["rate"] = 3 };

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json, parameters);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_coerce_a_supplied_long_value_for_a_number_parameter()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": { "rate": { "type": "number" } },
              "rule": { "spec": "is-positive", "whenTrue": "rate {rate}", "whenFalse": "no" }
            }
            """;
        var expected = Spec.Build(IsPositive).WhenTrue("rate 4000000000").WhenFalse("no").Create();
        var parameters = new Dictionary<string, object?> { ["rate"] = 4_000_000_000L };

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json, parameters);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_coerce_a_supplied_decimal_value_for_a_number_parameter()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": { "rate": { "type": "number" } },
              "rule": { "spec": "is-positive", "whenTrue": "rate {rate}", "whenFalse": "no" }
            }
            """;
        var expected = Spec.Build(IsPositive).WhenTrue("rate 2.5").WhenFalse("no").Create();
        var parameters = new Dictionary<string, object?> { ["rate"] = 2.5m };

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json, parameters);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_coerce_a_supplied_string_value_for_a_string_parameter()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": { "label": { "type": "string" } },
              "rule": { "spec": "is-positive", "whenTrue": "{label}", "whenFalse": "no" }
            }
            """;
        var expected = Spec.Build(IsPositive).WhenTrue("VIP").WhenFalse("no").Create();
        var parameters = new Dictionary<string, object?> { ["label"] = "VIP" };

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json, parameters);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_coerce_a_supplied_boolean_value_for_a_boolean_parameter()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": { "strict": { "type": "boolean" } },
              "rule": { "spec": "is-positive", "whenTrue": "strict {strict}", "whenFalse": "no" }
            }
            """;
        var expected = Spec.Build(IsPositive).WhenTrue("strict false").WhenFalse("no").Create();
        var parameters = new Dictionary<string, object?> { ["strict"] = false };

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json, parameters);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_format_a_false_boolean_default_parameter_invariantly()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": { "strict": { "type": "boolean", "default": false } },
              "rule": { "spec": "is-positive", "whenTrue": "strict {strict}", "whenFalse": "no" }
            }
            """;
        var expected = Spec.Build(IsPositive).WhenTrue("strict false").WhenFalse("no").Create();

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_not_report_unsupplied_required_parameters_of_every_type_during_validation()
    {
        // Arrange — validation stands in type-shaped placeholders for every declared type
        const string json =
            """
            {
              "parameters": {
                "rate": { "type": "number" },
                "strict": { "type": "boolean" },
                "label": { "type": "string" }
              },
              "rule": { "spec": "is-positive", "whenTrue": "{rate} {strict} {label}", "whenFalse": "no" }
            }
            """;

        // Act
        var errors = CreateSerializer().Validate<int>(json);

        // Assert
        errors.ShouldBeEmpty();
    }
}
