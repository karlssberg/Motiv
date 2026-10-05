namespace Motiv.Tests;

public class ThrowTests
{
    [Fact]
    public void Should_name_the_parameter_it_was_called_on_when_no_name_is_given()
    {
        // Arrange
        object? operand = null;

        // Act
        var act = () => operand.ThrowIfNull();

        // Assert
        var exception = act.ShouldThrow<ArgumentNullException>();
        exception.ParamName.ShouldBe<string?>("operand");
        exception.Message.ShouldContain("'operand' cannot be null");
    }

    [Fact]
    public void Should_prefer_an_explicit_name_over_the_expression()
    {
        // Arrange
        object? operand = null;

        // Act
        var act = () => operand.ThrowIfNull("spec");

        // Assert
        var exception = act.ShouldThrow<ArgumentNullException>();
        exception.ParamName.ShouldBe<string?>("spec");
        exception.Message.ShouldContain("'spec' cannot be null");
    }

    [Fact]
    public void Should_return_the_value_when_it_is_not_null()
    {
        // Arrange
        var operand = new object();

        // Act
        var result = operand.ThrowIfNull();

        // Assert
        result.ShouldBeSameAs(operand);
    }
}
