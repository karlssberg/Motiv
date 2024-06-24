namespace Motiv.Tests;

public class AsNSatisfiedSpecTests
{
    [Theory]
    [InlineData(2, 4, true)]
    [InlineData(4, 3, false)]
    [InlineData(1, 2, false)]
    [InlineData(1, 3, false)]
    public void Should_perform_the_logical_operation_NSatisfied(
        int first,
        int second,
        bool expected)
    {
        // Arrange
        var isEven = Spec
            .Build<int>(i => i % 2 == 0)
            .WhenTrue(i => $"{i.ToString()} is even")
            .WhenFalse(i => $"{i.ToString()} is odd")
            .Create("is even spec");

        var spec = Spec
            .Build(isEven)
            .AsNSatisfied(2)
            .WhenTrue("is a pair of even numbers")
            .WhenFalse("is not a pair of even numbers")
            .Create();

        var result = spec.IsSatisfiedBy([first, second]);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(1, 3, 5, 7, false)]
    [InlineData(1, 3, 5, 6, false)]
    [InlineData(1, 3, 4, 6, true)]
    [InlineData(1, 4, 6, 8, false)]
    public void Should_satisfy_n_satisfied_spec_when_handling_metadata(
        int first,
        int second,
        int third,
        int fourth,
        bool expected)
    {
        // Arrange
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .WhenTrue(n => $"{n.ToString()} is even")
            .WhenFalse(n => $"{n.ToString()} is odd")
            .Create("is even spec");

        var spec = Spec
            .Build(isEven)
            .AsNSatisfied(2)
            .WhenTrue((results) =>
                $"{string.Join(", ", results.CausalModels)} are a pair of even numbers")
            .WhenFalse("The pack does not contain exactly a pair of even numbers")
            .Create("a pair of even numbers");

        var result = spec.IsSatisfiedBy([first, second, third, fourth]);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(1, 3, 5, 7, "The pack does not contain exactly a pair of even numbers")]
    [InlineData(1, 3, 5, 6, "The pack does not contain exactly a pair of even numbers")]
    [InlineData(1, 3, 4, 6, "4, 6 are a pair of even numbers")]
    [InlineData(1, 4, 6, 8, "The pack does not contain exactly a pair of even numbers")]
    public void Should_assert_the_outcome_of_an_n_satisfied_spec_metadata(
        int first,
        int second,
        int third,
        int fourth,
        string expectedShallowAssertionSerialized)
    {
        // Arrange
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .WhenTrue(n => $"{n.ToString()} is even")
            .WhenFalse(n => $"{n.ToString()} is odd")
            .Create("is even spec");

        var spec = Spec
            .Build(isEven)
            .AsNSatisfied(2)
            .WhenTrue((results) =>
                $"{string.Join(", ", results.CausalModels)} are a pair of even numbers")
            .WhenFalse("The pack does not contain exactly a pair of even numbers")
            .Create("a pair of even numbers");

        var result = spec.IsSatisfiedBy([first, second, third, fourth]);

        // Act
        var act = result.Assertions;
        
        // Assert
        act.Should().BeEquivalentTo(expectedShallowAssertionSerialized);
    }
    
    [Theory]
    [InlineData(1, 3, 5, 7, "1 is odd, 3 is odd, 5 is odd, 7 is odd")]
    [InlineData(1, 3, 5, 6, "6 is even")]
    [InlineData(1, 3, 4, 6, "4 is even, 6 is even")]
    [InlineData(1, 4, 6, 8, "4 is even, 6 is even, 8 is even")]
    public void Should_provide_an_underlying_explanation_of_an_n_satisfied_spec_metadata(
        int first,
        int second,
        int third,
        int fourth,
        string expectedDeepAssertionsSerialized)
    {
        // Arrange
        var expectedDeepAssertions = expectedDeepAssertionsSerialized.Split(new []{", "}, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .WhenTrue(n => $"{n.ToString()} is even")
            .WhenFalse(n => $"{n.ToString()} is odd")
            .Create("is even spec");

        var spec = Spec
            .Build(isEven)
            .AsNSatisfied(2)
            .WhenTrue((results) =>
                $"{string.Join(", ", results.CausalModels)} are a pair of even numbers")
            .WhenFalse("The pack does not contain exactly a pair of even numbers")
            .Create("a pair of even numbers");

        var result = spec.IsSatisfiedBy([first, second, third, fourth]);

        // Act
        var act = result.Explanation.Underlying;
        
        // Assert
        act.GetAssertions().Should().BeEquivalentTo(expectedDeepAssertions);
    }
    
    [Theory]
    [InlineAutoData(true, true, "2 even")]
    [InlineAutoData(true, false, "1 even and 1 odd")]
    [InlineAutoData(false, true, "1 even and 1 odd")]
    [InlineAutoData(false, false, "0 even and 2 odd")]
    public void Should_serialize_the_result_of_the_n_satisfied_operation_when_metadata_is_a_string(
        bool first,
        bool second,
        string expected)
    {
        // Arrange
        var isEven = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString().ToLowerInvariant())
            .WhenFalse(false.ToString().ToLowerInvariant())
            .Create();

        var spec = Spec
            .Build(isEven)
            .AsNSatisfied(2)
            .WhenTrue("2 even")
            .WhenFalse(evaluation => $"{evaluation.TrueCount} even and {evaluation.FalseCount} odd")
            .Create();
        
        var result = spec.IsSatisfiedBy([first, second]);

        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Fact]
    public void Should_describe_an_NSatisfied_spec()
    {
        // Arrange
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("is even");

        var spec = Spec
            .Build(isEven)
            .AsNSatisfied(2)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("a pair of even numbers");

        // Act
        var act = spec.Statement;
        
        // Assert
        act.Should().Be("a pair of even numbers");
    }
    
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public void Should_perform_an_n_satisfied_operation_when_using_a_boolean_predicate_function(
        bool first,
        bool second,
        bool third,
        bool expected)
    {
        // Arrange
        var spec =
            Spec.Build((bool m) => m)
                .AsNSatisfied(2)
                .Create("2 are true");

        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]   [InlineData(false, false, false, "!2 are true")]
    [InlineData(false, false, true, "!2 are true")]
    [InlineData(false, true, false, "!2 are true")]
    [InlineData(false, true, true, "2 are true")]
    [InlineData(true, false, false, "!2 are true")]
    [InlineData(true, false, true, "2 are true")]
    [InlineData(true, true, false, "2 are true")]
    [InlineData(true, true, true, "!2 are true")]
    public void Should_perform_a_none_satisfied_operation_when_using_a_boolean_predicate_function(
        bool first,
        bool second,
        bool third,
        string expectedReason)
    {
        // Arrange
        var spec =
            Spec.Build((bool m) => m)
                .AsNSatisfied(2)
                .Create("2 are true");

        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expectedReason);
    }
    
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public void Should_perform_an_n_satisfied_operation_when_using_a_boolean_result_predicate_function_with_metadata(
        bool first,
        bool second,
        bool third,
        bool expected)
    {
        // Arrange
        var spec =
            Spec.Build((bool m) => m)
                .AsNSatisfied(2)
                .WhenTrue(_ => "2 are true")
                .WhenFalse(_ => "!2 are true")
                .Create("none are true");

        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(false, false, false, "!2 are true")]
    [InlineData(false, false, true, "!2 are true")]
    [InlineData(false, true, false, "!2 are true")]
    [InlineData(false, true, true, "2 are true")]
    [InlineData(true, false, false, "!2 are true")]
    [InlineData(true, false, true, "2 are true")]
    [InlineData(true, true, false, "2 are true")]
    [InlineData(true, true, true, "!2 are true")]
    public void Should_provide_a_reason_for_an_n_satisfied_operation_when_using_a_boolean_result_predicate_function_with_metadata(
        bool first,
        bool second,
        bool third,       string expectedReason)
    {
        // Arrange
        var spec =
            Spec.Build((bool m) => m)
                .AsNSatisfied(2)
                .WhenTrue(_ => "2 are true")
                .WhenFalse(_ => "!2 are true")
                .Create("none are true");

        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expectedReason);
    }
    
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, true, false)]   [InlineData(false, true, false, false)]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public void Should_perform_an_n_satisfied_operation_when_using_a_boolean_result_predicate_function(
        bool first,
        bool second,
        bool third,
        bool expected)
    {
        // Arrange
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is true");

        var spec =
            Spec.Build((bool model) => underlying.IsSatisfiedBy(model))
                .AsNSatisfied(2)
                .WhenTrue(_ => "2 are true")
                .WhenFalse(_ => "!2 are true")
                .Create("2 are true");

        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(false, false, false, "!2 are true")]
    [InlineData(false, false, true, "!2 are true")]
    [InlineData(false, true, false, "!2 are true")]
    [InlineData(false, true, true, "2 are true")]
    [InlineData(true, false, false, "!2 are true")]
    [InlineData(true, false, true, "2 are true")]
    [InlineData(true, true, false, "2 are true")]
    [InlineData(true, true, true, "!2 are true")]
    public void Should_provide_a_reason_an_n_satisfied_operation_when_using_a_boolean_result_predicate_function(
        bool first,
        bool second,
        bool third,
        string expectedReason)
    {
        // Arrange
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is true");

        var spec =
            Spec.Build((bool model) => underlying.IsSatisfiedBy(model))
                .AsNSatisfied(2)
                .WhenTrue(_ => "2 are true")
                .WhenFalse(_ => "!2 are true")
                .Create("2 are true");

        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expectedReason);
    }
}