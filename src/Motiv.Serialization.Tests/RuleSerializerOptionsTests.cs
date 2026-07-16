namespace Motiv.Serialization.Tests;

public class RuleSerializerOptionsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_reject_a_MaxDocumentDepth_below_one(int value)
    {
        // Arrange
        var options = new RuleSerializerOptions();

        // Act
        var act = () => { options.MaxDocumentDepth = value; };

        // Assert
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_reject_a_MaxNodeCount_below_one(int value)
    {
        // Arrange
        var options = new RuleSerializerOptions();

        // Act
        var act = () => { options.MaxNodeCount = value; };

        // Assert
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Should_validate_without_overflowing_when_MaxDocumentDepth_is_int_MaxValue()
    {
        // Arrange
        var options = new RuleSerializerOptions { MaxDocumentDepth = int.MaxValue };
        var serializer = new RuleSerializer(new SpecRegistry(), options);

        // Act
        var errors = serializer.Validate("""{ "rule": { "spec": "a" } }""");

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Should_reject_a_null_registry()
    {
        // Act
        var act = () => new RuleSerializer(null!);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }
}
