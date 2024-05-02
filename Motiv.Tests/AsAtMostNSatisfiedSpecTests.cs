using FluentAssertions;

namespace Motiv.Tests;

public class AsAtMostNSatisfiedSpecTests
{
    [Theory]
    [InlineAutoData(false, false, false, false, true )]
    [InlineAutoData(false, false, false, true,  false)]
    [InlineAutoData(false, false, true,  false, false)]
    [InlineAutoData(false, false, true,  true,  false)]
    [InlineAutoData(false, true,  false, false, false)]
    [InlineAutoData(false, true,  false, true,  false)]
    [InlineAutoData(false, true,  true,  false, false)]
    [InlineAutoData(false, true,  true,  true,  false)]
    [InlineAutoData(true,  false, false, false, false)]
    [InlineAutoData(true,  false, false, true,  false)]
    [InlineAutoData(true,  false, true,  false, false)]
    [InlineAutoData(true,  false, true,  true,  false)]
    [InlineAutoData(true,  true,  false, false, false)]
    [InlineAutoData(true,  true,  false, true,  false)]
    [InlineAutoData(true,  true,  true,  false, false)]
    [InlineAutoData(true,  true,  true,  true,  false)]
    public void Should_perform_the_logical_operation_at_most_when_0_is_supplied_as_the_maximum(
        bool first,
        bool second,
        bool third,
        bool fourth,
        bool expected)
    {
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("returns the model");

        var spec = Spec
            .Build(underlyingSpec)
            .AsAtMostNSatisfied(0)
            .WhenTrue("none are satisfied")
            .WhenFalse("one or more are satisfied")
            .Create();
        
        var result = spec.IsSatisfiedBy([first, second, third, fourth]);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, false, true )]
    [InlineAutoData(false, false, false, true,  true )]
    [InlineAutoData(false, false, true,  false, true )]
    [InlineAutoData(false, false, true,  true,  false)]
    [InlineAutoData(false, true,  false, false, true )]
    [InlineAutoData(false, true,  false, true,  false)]
    [InlineAutoData(false, true,  true,  false, false)]
    [InlineAutoData(false, true,  true,  true,  false)]
    [InlineAutoData(true,  false, false, false, true )]
    [InlineAutoData(true,  false, false, true,  false)]
    [InlineAutoData(true,  false, true,  false, false)]
    [InlineAutoData(true,  false, true,  true,  false)]
    [InlineAutoData(true,  true,  false, false, false)]
    [InlineAutoData(true,  true,  false, true,  false)]
    [InlineAutoData(true,  true,  true,  false, false)]
    [InlineAutoData(true,  true,  true,  true,  false)]
    public void Should_perform_the_logical_operation_at_most_when_1_is_supplied_as_the_maximum(
        bool first,
        bool second,
        bool third,
        bool fourth,
        bool expected)
    {
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("returns the model");

        var spec = Spec.Build(underlyingSpec)
            .AsAtMostNSatisfied(1)
            .WhenTrue("one is satisfied")
            .WhenFalse("none or more than one is not satisfied")
            .Create();
        
        var result = spec.IsSatisfiedBy([first, second, third, fourth]);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, false, true )]
    [InlineAutoData(false, false, false, true,  true )]
    [InlineAutoData(false, false, true,  false, true )]
    [InlineAutoData(false, false, true,  true,  true )]
    [InlineAutoData(false, true,  false, false, true )]
    [InlineAutoData(false, true,  false, true,  true )]
    [InlineAutoData(false, true,  true,  false, true )]
    [InlineAutoData(false, true,  true,  true,  false)]
    [InlineAutoData(true,  false, false, false, true )]
    [InlineAutoData(true,  false, false, true,  true )]
    [InlineAutoData(true,  false, true,  false, true )]
    [InlineAutoData(true,  false, true,  true,  false)]
    [InlineAutoData(true,  true,  false, false, true )]
    [InlineAutoData(true,  true,  false, true,  false)]
    [InlineAutoData(true,  true,  true,  false, false)]
    [InlineAutoData(true,  true,  true,  true,  false)]
    public void Should_perform_the_logical_operation_at_most_when_2_is_supplied_as_the_maximum(
        bool first,
        bool second,
        bool third,
        bool fourth,
        bool expected)
    {
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("returns the model");

        var spec = Spec.Build(underlyingSpec)
            .AsAtMostNSatisfied(2)
            .WhenTrue("at most two are satisfied")
            .WhenFalse("more than two are satisfied")
            .Create();
        
        var result = spec.IsSatisfiedBy([first, second, third, fourth]);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, false, true )]
    [InlineAutoData(false, false, false, true,  true )]
    [InlineAutoData(false, false, true,  false, true )]
    [InlineAutoData(false, false, true,  true,  true )]
    [InlineAutoData(false, true,  false, false, true )]
    [InlineAutoData(false, true,  false, true,  true )]
    [InlineAutoData(false, true,  true,  false, true )]
    [InlineAutoData(false, true,  true,  true,  true )]
    [InlineAutoData(true,  false, false, false, true )]
    [InlineAutoData(true,  false, false, true,  true )]
    [InlineAutoData(true,  false, true,  false, true )]
    [InlineAutoData(true,  false, true,  true,  true )]
    [InlineAutoData(true,  true,  false, false, true )]
    [InlineAutoData(true,  true,  false, true,  true )]
    [InlineAutoData(true,  true,  true,  false, true )]
    [InlineAutoData(true,  true,  true,  true,  true )]
    public void Should_perform_the_logical_operation_at_most_when_the_set_size_is_supplied_as_the_maximum(
        bool first,
        bool second,
        bool third,
        bool fourth,
        bool expected)
    {
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("returns the model");

        var spec = Spec
            .Build(underlyingSpec)
            .AsAtMostNSatisfied(4)
            .WhenTrue("at most four are satisfied")
            .WhenFalse("more than four are satisfied")
            .Create();
        
        var result = spec.IsSatisfiedBy([first, second, third, fourth]);

        // Act  
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                            at most one is satisfied
                                                is not satisfied
                                                is not satisfied
                                                is not satisfied
                                            """)]
    [InlineAutoData(false, false, true,  """
                                            at most one is satisfied
                                                is satisfied
                                            """)]
    [InlineAutoData(false, true,  false, """
                                            at most one is satisfied
                                                is satisfied
                                            """)]
    [InlineAutoData(false, true,  true,  """
                                            more than one is satisfied
                                                is satisfied
                                                is satisfied
                                            """)]
    [InlineAutoData(true,  false, false, """
                                            at most one is satisfied
                                                is satisfied
                                            """)]
    [InlineAutoData(true,  false, true,  """
                                            more than one is satisfied
                                                is satisfied
                                                is satisfied
                                            """)]
    [InlineAutoData(true,  true,  false, """
                                            more than one is satisfied
                                                is satisfied
                                                is satisfied
                                            """)]
    [InlineAutoData(true,  true,  true,  """
                                            more than one is satisfied
                                                is satisfied
                                                is satisfied
                                                is satisfied
                                            """)]
    public void Should_serialize_the_result_of_the_at_most_of_1_operation_when_metadata_is_a_string(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue("is satisfied")
            .WhenFalse("is not satisfied")
            .Create();

        var spec = Spec
            .Build(underlyingSpec)
            .AsAtMostNSatisfied(1)
            .WhenTrue("at most one is satisfied")
            .WhenFalse("more than one is satisfied")
            .Create();
        
        var result = spec.IsSatisfiedBy([first, second, third]);
        
        // Act
        var act = result.Justification;
        
        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                            at most one is satisfied
                                                False
                                                False
                                                False
                                            """)]
    [InlineAutoData(false, false, true, """
                                            at most one is satisfied
                                                True
                                            """)]
    [InlineAutoData(false, true,  false, """
                                            at most one is satisfied
                                                True
                                            """)]
    [InlineAutoData(false, true,  true, """
                                            more than one is satisfied
                                                True
                                                True
                                            """)]
    [InlineAutoData(true,  false, false, """
                                            at most one is satisfied
                                                True
                                            """)]
    [InlineAutoData(true,  false, true, """
                                            more than one is satisfied
                                                True
                                                True
                                            """)]
    [InlineAutoData(true,  true,  false, """
                                            more than one is satisfied
                                                True
                                                True
                                            """)]
    [InlineAutoData(true,  true,  true, """
                                            more than one is satisfied
                                                True
                                                True
                                                True
                                            """)]
    public void Should_serialize_the_result_of_the_at_most_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var spec = Spec
            .Build(underlyingSpec)
            .AsAtMostNSatisfied(1)
            .WhenTrue("at most one is satisfied")
            .WhenFalse("more than one is satisfied")
            .Create();
        
        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Justification;
        
        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                            at most one is satisfied
                                                !is true
                                                !is true
                                                !is true
                                            """)]
    [InlineAutoData(false, false, true,  """
                                            at most one is satisfied
                                                is true
                                            """)]
    [InlineAutoData(false, true,  false, """
                                            at most one is satisfied
                                                is true
                                            """)]
    [InlineAutoData(false, true,  true,  """
                                            more than one is satisfied
                                                is true
                                                is true
                                            """)]
    [InlineAutoData(true,  false, false, """
                                            at most one is satisfied
                                                is true
                                            """)]
    [InlineAutoData(true,  false, true,  """
                                            more than one is satisfied
                                                is true
                                                is true
                                            """)]
    [InlineAutoData(true,  true,  false, """
                                            more than one is satisfied
                                                is true
                                                is true
                                            """)]
    [InlineAutoData(true,  true,  true,  """
                                            more than one is satisfied
                                                is true
                                                is true
                                                is true
                                            """)]
    public void Should_serialize_the_result_of_the_all_operation(
        bool first, 
        bool second,
        bool third,
        string expected)
    {
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("is true");

        var spec = Spec
            .Build(underlyingSpec)
            .AsAtMostNSatisfied(1)
            .WhenTrue("at most one is satisfied")
            .WhenFalse("more than one is satisfied")
            .Create();
        
        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Justification;
        
        // Assert
        act.Should().Be(expected);
    }

    [Fact]
    public void Should_provide_a_description_of_the_specification()
    {
        // Arrange
        const string expected = "at most one is satisfied";
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue("underlying model is true")
            .WhenFalse("underlying model is false")
            .Create("underlying spec description");

        var spec = Spec
            .Build(underlyingSpec)
            .AsAtMostNSatisfied(1)
            .WhenTrue("at most one is satisfied")
            .WhenFalse("more than one is satisfied")
            .Create();

        // Act
        var act = spec.Statement;
        
        // Assert
        act.Should().Be(expected);
    }

    [Fact]
    public void Should_serialize_the_specification()
    {
        // Arrange
        const string expected = "at most one is satisfied";
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue("underlying model is true")
            .WhenFalse("underlying model is false")
            .Create("underlying spec description");

        var spec = Spec
            .Build(underlyingSpec)
            .AsAtMostNSatisfied(1)
            .WhenTrue("at most one is satisfied")
            .WhenFalse("more than one is satisfied")
            .Create();

        // Act
        var act = spec.ToString();
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(false, false, false, 3)]
    [InlineAutoData(false, false, true, 1)]
    [InlineAutoData(false, true, false, 1)]
    [InlineAutoData(false, true, true, 2)]
    [InlineAutoData(true, false, false, 1)]
    [InlineAutoData(true, false, true, 2)]
    [InlineAutoData(true, true, false, 2)]
    [InlineAutoData(true, true, true, 3)]
    public void Should_accurately_report_the_number_of_causal_operands(
        bool firstModel,
        bool secondModel,
        bool thirdModel,
        int expected)
    {
        // Arrange
        var underlying = Spec
            .Build((bool m) => m)
            .Create("underlying");
        
        var spec = Spec
            .Build(underlying)
            .AsAtMostNSatisfied(2)
            .Create("all are true");

        var result = spec.IsSatisfiedBy([firstModel, secondModel, thirdModel]);

        // Act
        var act = result.Description.CausalOperandCount;
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, false, true)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, false)]
    public void Should_perform_an_at_most_n_satisfied_operation_when_using_a_boolean_predicate_function(
        bool first,
        bool second,
        bool third,
        bool expected)
    {
        // Arrange
        var spec =
            Spec.Build((bool m) => m)
                .AsAtMostNSatisfied(1)
                .WhenTrue(_ => "at most 1 true")
                .WhenFalse(_ => "!at most 1 true")
                .Create("at most 1 true");

        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(false, false, false, "at most 1 true")]
    [InlineData(false, false, true, "at most 1 true")]
    [InlineData(false, true, false, "at most 1 true")]
    [InlineData(false, true, true, "!at most 1 true")]
    [InlineData(true, false, false, "at most 1 true")]
    [InlineData(true, false, true, "!at most 1 true")]
    [InlineData(true, true, false, "!at most 1 true")]
    [InlineData(true, true, true, "!at most 1 true")]
    public void Should_provide_a_reason_for_an_at_most_n_satisfied_operation_when_using_a_boolean_predicate_function(
        bool first,
        bool second,
        bool third,
        string expectedReason)
    {
        // Arrange
        var spec =
            Spec.Build((bool m) => m)
                .AsAtMostNSatisfied(1)
                .WhenTrue(_ => "at most 1 true")
                .WhenFalse(_ => "!at most 1 true")
                .Create("at most 1 true");

        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Reason;
        
        // Assert
        act.Should().Be(expectedReason);
    }
    
    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, false, true)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, false)]
    public void Should_perform_an_at_most_n_satisfied_operation_when_using_a_boolean_result_predicate_function(
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
                .AsAtMostNSatisfied(1)
                .WhenTrue(_ => "at most 1 true")
                .WhenFalse(_ => "!at most 1 true")
                .Create("at most 1 true");

        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(false, false, false, "at most 1 true")]
    [InlineData(false, false, true, "at most 1 true")]
    [InlineData(false, true, false, "at most 1 true")]
    [InlineData(false, true, true, "!at most 1 true")]
    [InlineData(true, false, false, "at most 1 true")]
    [InlineData(true, false, true, "!at most 1 true")]
    [InlineData(true, true, false, "!at most 1 true")]
    [InlineData(true, true, true, "!at most 1 true")]
    public void Should_provide_a_reason_for_an_at_most_n_satisfied_operation_when_using_a_boolean_result_predicate_function(
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
                .AsAtMostNSatisfied(1)
                .WhenTrue(_ => "at most 1 true")
                .WhenFalse(_ => "!at most 1 true")
                .Create("at most 1 true");

        var result = spec.IsSatisfiedBy([first, second, third]);

        var act = result.Reason;
        
        act.Should().Be(expectedReason);
    }
}