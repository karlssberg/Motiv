using Motiv.Serialization;

namespace Motiv.Serialization.Tests;

public class RuleSerializationExceptionTests
{
    [Fact]
    public void Should_use_the_single_error_as_the_message_when_there_is_one_error()
    {
        // Arrange
        var error = new RuleError("$.rule", RuleErrorCode.UnknownSpec, "no spec is registered under the name 'x'");

        // Act
        var exception = new RuleSerializationException([error]);

        // Assert
        exception.Message.ShouldBe(error.ToString());
    }

    [Fact]
    public void Should_summarise_the_first_error_and_a_count_when_there_are_many_errors()
    {
        // Arrange
        var first = new RuleError("$.rule", RuleErrorCode.UnknownSpec, "first");
        var second = new RuleError("$.rule.and[1]", RuleErrorCode.InvalidNode, "second");
        var third = new RuleError("$.name", RuleErrorCode.InvalidNode, "third");

        // Act
        var exception = new RuleSerializationException([first, second, third]);

        // Assert
        exception.Message.ShouldBe($"{first} (+2 more)");
    }
}
