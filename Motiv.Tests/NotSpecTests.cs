namespace Motiv.Tests;

public class NotSpecTests
{
    [Theory]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    public void Should_perform_logical_not(
        bool operand,
        bool expected,
        object model)
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => operand)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create($"is {operand}");

        var result = (!spec).IsSatisfiedBy(model);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_yield_metadata(
        bool operand,
        object model)
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => operand)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create($"is {operand}");

        var result = (!spec).IsSatisfiedBy(model);

        // Act
        var act = result.Metadata;
        
        // Assert
        act.Should().AllBeEquivalentTo(operand);
    }

    [Theory]
    [InlineAutoData(true, "is true")]
    [InlineAutoData(false, "!is true")]
    public void Should_serialize_the_result_of_the_not_operation(
        bool operand,
        string expected,
        object model)
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => operand)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("is true");

        var result = (!spec).IsSatisfiedBy(model);

        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(true, "True")]
    [InlineAutoData(false, "False")]
    public void Should_serialize_the_result_of_the_not_operation_when_metadata_is_a_string(
        bool operand,
        string expected,
        object model)
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => operand)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var result = (!spec).IsSatisfiedBy(model);

        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("is true", "!is true")]
    [InlineData("!is true", "is true")]
    [InlineData("(is true)", "!(is true)")]
    public void Should_Format_Statement_Correctly(string operandStatement, string expected)
    {
        // Arrange
        var operand = Spec
            .Build<object>(_ => true)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create(operandStatement);

        var notSpecDescription = !operand;

        // Act
        var statement = notSpecDescription.Statement;

        // Assert
        statement.Should().Be(expected);
    }

    [Fact]
    public void Should_Format_Statement_Correctly_When_Operand_Is_BinaryOperationSpec()
    {
        // Arrange
        var left = 
            Spec.Build<object>(_ => true)
                .WhenTrue(true)
                .WhenFalse(false)
                .Create("is true");
        
        var right =
            Spec.Build<object>(_ => false)
                .WhenTrue(true)
                .WhenFalse(false)
                .Create("is false");

        var notSpecDescription = !(left & right);

        // Act
        var statement = notSpecDescription.Statement;

        // Assert
        statement.Should().Be("!(is true & is false)");
    }
    

    [Fact]
    public void Should_return_the_underlying_specs()
    {
        // Arrange
        var underlying = Spec
            .Build<bool>(_ => true)
            .Create("left");

        // Act
        var act = (!underlying).Underlying;
        
        // Assert
        act.Should().BeEquivalentTo([underlying]);
    }
    
    [Fact]
    public void Should_populate_underlying_results_with_metadata()
    {
        // Arrange
        var underlyingSpec = Spec.Build<object>(_ => true).Create("left");
        var expected = underlyingSpec.IsSatisfiedBy(new object());
        
        var spec = !underlyingSpec;
        var result = spec.IsSatisfiedBy(new object());
        
        // Act
        var act = result.UnderlyingWithMetadata;
        
        // Assert
        act.Should().BeEquivalentTo([expected]);
    }
}