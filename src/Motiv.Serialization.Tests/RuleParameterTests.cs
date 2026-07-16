namespace Motiv.Serialization.Tests;

public class RuleParameterTests
{
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
}
