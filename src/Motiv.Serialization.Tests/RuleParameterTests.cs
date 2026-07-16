namespace Motiv.Serialization.Tests;

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
}
