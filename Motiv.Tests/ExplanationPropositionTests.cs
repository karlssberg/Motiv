namespace Motiv.Tests;

public class ExplanationPropositionTests
{
    [Theory]
    [InlineData(1, "is odd")]
    [InlineData(2, "is even")]
    public void Should_use_the_defined_assertion_as_the_assertion_result(int model, string expected)
    {
        // Arrange
        var spec = Spec.Build((int i) => i % 2 == 0)
            .WhenTrue("is even")
            .WhenFalse("is odd")
            .Create();
        
        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Assertions;
        
        // Assert
        act.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(1, "is odd")]
    [InlineData(2, "is even")]
    public void Should_use_the_defined_assertion_as_the_reason_for_the_result(int model, string expected)
    {
        // Arrange
        var spec = Spec.Build((int i) => i % 2 == 0)
            .WhenTrue("is even")
            .WhenFalse("is odd")
            .Create();
        
        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineData(1, "odd")]
    [InlineData(2, "even")]
    public void Should_ensure_that_the_propositional_statement_is_not_used_as_the_reason(int model, string expected)
    {
        // Arrange
        var spec = Spec.Build((int i) => i % 2 == 0)
            .WhenTrue("even")
            .WhenFalse("odd")
            .Create("is even");
        
        var result = spec.IsSatisfiedBy(model);
        
        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expected);
    }

    [Fact]
    public void Should_return_the_propositional_statement()
    {
        // Arrange
        var spec = Spec.Build((int i) => i % 2 == 0)
            .WhenTrue("even")
            .WhenFalse("odd")
            .Create("is even");

        // Act
        var act = spec.Statement;
        
        // Assert
        act.Should().Be("is even");
    }
    
    [Theory]
    [InlineData(true,  "first is true", "second is true", "third is true")]
    [InlineData(false, "first is false", "second is false", "third is false")]
    public void Should_allow_the_yielding_of_multiple_assertions(
        bool model,
        params string[] expectedAssertions)
    {
        // Arrange
        var firstSpec = Spec
            .Build((bool m) => m)
            .WhenTrueYield(_ => ["first is true"])
            .WhenFalse("first is false")
            .Create("first true");

        var secondSpec = Spec
            .Build((bool m) => m)
            .WhenTrue("second is true")
            .WhenFalseYield(_ => ["second is false"])
            .Create("second true");

        var thirdSpec = Spec
            .Build((bool m) => m)
            .WhenTrueYield(_ => ["third is true"])
            .WhenFalseYield(_ => ["third is false"])
            .Create("third true");

        var spec = firstSpec | secondSpec | thirdSpec;

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Assertions;
        
        // Assert
        act.Should().BeEquivalentTo(expectedAssertions);
    }
    
}