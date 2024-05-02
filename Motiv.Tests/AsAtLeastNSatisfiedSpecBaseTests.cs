using FluentAssertions;

namespace Motiv.Tests;

public class AsAtLeastNSatisfiedSpecBaseTests
{
    [Theory]
    [InlineAutoData(false, false, false, false, true)]
    [InlineAutoData(false, false, false, true, true)]
    [InlineAutoData(false, false, true, false, true)]
    [InlineAutoData(false, false, true, true, true)]
    [InlineAutoData(false, true, false, false, true)]
    [InlineAutoData(false, true, false, true, true)]
    [InlineAutoData(false, true, true, false, true)]
    [InlineAutoData(false, true, true, true, true)]
    [InlineAutoData(true, false, false, false, true)]
    [InlineAutoData(true, false, false, true, true)]
    [InlineAutoData(true, false, true, false, true)]
    [InlineAutoData(true, false, true, true, true)]
    [InlineAutoData(true, true, false, false, true)]
    [InlineAutoData(true, true, false, true, true)]
    [InlineAutoData(true, true, true, false, true)]
    [InlineAutoData(true, true, true, true, true)]
    public void Should_perform_the_logical_operation_at_least_when_0_is_supplied_as_the_minimum(
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
            .AsAtLeastNSatisfied(0)
            .WhenTrue("none satisfied")
            .WhenFalse("at least one satisfied")
            .Create();
        
        var result = spec.IsSatisfiedBy([first, second, third, fourth]);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, false, false)]
    [InlineAutoData(false, false, false, true, true)]
    [InlineAutoData(false, false, true, false, true)]
    [InlineAutoData(false, false, true, true, true)]
    [InlineAutoData(false, true, false, false, true)]
    [InlineAutoData(false, true, false, true, true)]
    [InlineAutoData(false, true, true, false, true)]
    [InlineAutoData(false, true, true, true, true)]
    [InlineAutoData(true, false, false, false, true)]
    [InlineAutoData(true, false, false, true, true)]
    [InlineAutoData(true, false, true, false, true)]
    [InlineAutoData(true, false, true, true, true)]
    [InlineAutoData(true, true, false, false, true)]
    [InlineAutoData(true, true, false, true, true)]
    [InlineAutoData(true, true, true, false, true)]
    [InlineAutoData(true, true, true, true, true)]
    public void Should_perform_the_logical_operation_at_least_when_1_is_supplied_as_the_minimum(
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
            .AsAtLeastNSatisfied(1)
            .WhenTrue("One satisfied")
            .WhenFalse("None or more than one satisfied")
            .Create();
        
        var result = spec.IsSatisfiedBy([first, second, third, fourth]);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, false, false)]
    [InlineAutoData(false, false, false, true, false)]
    [InlineAutoData(false, false, true, false, false)]
    [InlineAutoData(false, false, true, true, true)]
    [InlineAutoData(false, true, false, false, false)]
    [InlineAutoData(false, true, false, true, true)]
    [InlineAutoData(false, true, true, false, true)]
    [InlineAutoData(false, true, true, true, true)]
    [InlineAutoData(true, false, false, false, false)]
    [InlineAutoData(true, false, false, true, true)]
    [InlineAutoData(true, false, true, false, true)]
    [InlineAutoData(true, false, true, true, true)]
    [InlineAutoData(true, true, false, false, true)]
    [InlineAutoData(true, true, false, true, true)]
    [InlineAutoData(true, true, true, false, true)]
    [InlineAutoData(true, true, true, true, true)]
    public void Should_perform_the_logical_operation_at_least_when_2_is_supplied_as_the_minimum(
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
            .AsAtLeastNSatisfied(2)
            .WhenTrue("At least two satisfied")
            .WhenFalse("Less than two satisfied")
            .Create();
        
        var result = spec.IsSatisfiedBy([first, second, third, fourth]);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, false, false)]
    [InlineAutoData(false, false, false, true, false)]
    [InlineAutoData(false, false, true, false, false)]
    [InlineAutoData(false, false, true, true, false)]
    [InlineAutoData(false, true, false, false, false)]
    [InlineAutoData(false, true, false, true, false)]
    [InlineAutoData(false, true, true, false, false)]
    [InlineAutoData(false, true, true, true, false)]
    [InlineAutoData(true, false, false, false, false)]
    [InlineAutoData(true, false, false, true, false)]
    [InlineAutoData(true, false, true, false, false)]
    [InlineAutoData(true, false, true, true, false)]
    [InlineAutoData(true, true, false, false, false)]
    [InlineAutoData(true, true, false, true, false)]
    [InlineAutoData(true, true, true, false, false)]
    [InlineAutoData(true, true, true, true, true)]
    public void Should_perform_the_logical_operation_at_least_when_the_set_size_is_supplied_as_the_minimum(
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
            .AsAtLeastNSatisfied(4)
            .WhenTrue("All satisfied")
            .WhenFalse("Not all satisfied")
            .Create();
        
        var result = spec.IsSatisfiedBy([first, second, third, fourth]);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                            none satisfied
                                                received false
                                                received false
                                                received false
                                            """)]
    [InlineAutoData(false, false, true,  """
                                            at least one satisfied
                                                received true
                                            """)]
    [InlineAutoData(false, true,  false, """
                                            at least one satisfied
                                                received true
                                            """)]
    [InlineAutoData(false, true,  true,  """
                                            at least one satisfied
                                                received true
                                                received true
                                            """)]
    [InlineAutoData(true,  false, false, """
                                            at least one satisfied
                                                received true
                                            """)]
    [InlineAutoData(true,  false, true,  """
                                            at least one satisfied
                                                received true
                                                received true
                                            """)]
    [InlineAutoData(true,  true,  false, """
                                            at least one satisfied
                                                received true
                                                received true
                                            """)]
    [InlineAutoData(true,  true,  true,  """
                                            at least one satisfied
                                                received true
                                                received true
                                                received true
                                            """)]
    public void Should_serialize_the_result_of_the_at_least_of_1_operation_when_metadata_is_a_string(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue("received true")
            .WhenFalse("received false")
            .Create();

        var spec = Spec
            .Build(underlyingSpec)
            .AsAtLeastNSatisfied(1)
            .WhenTrue("at least one satisfied")
            .WhenFalse("none satisfied")
            .Create();
            
        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Justification;
        
        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                            None satisfied
                                                underlying not satisfied
                                                underlying not satisfied
                                                underlying not satisfied
                                            """)]
    [InlineAutoData(false, false, true,  """
                                            At least one satisfied
                                                underlying satisfied
                                            """)]
    [InlineAutoData(false, true, false,  """
                                            At least one satisfied
                                                underlying satisfied
                                            """)]
    [InlineAutoData(false, true, true,   """
                                            At least one satisfied
                                                underlying satisfied
                                                underlying satisfied
                                            """)]
    [InlineAutoData(true, false, false,  """
                                            At least one satisfied
                                                underlying satisfied
                                            """)]
    [InlineAutoData(true, false, true,   """
                                            At least one satisfied
                                                underlying satisfied
                                                underlying satisfied
                                            """)]
    [InlineAutoData(true, true, false,   """
                                            At least one satisfied
                                                underlying satisfied
                                                underlying satisfied
                                            """)]
    [InlineAutoData(true, true, true,    """
                                            At least one satisfied
                                                underlying satisfied
                                                underlying satisfied
                                                underlying satisfied
                                            """)]
    public void Should_serialize_the_result_of_the_at_least_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue("underlying satisfied")
            .WhenFalse("underlying not satisfied")
            .Create();

        var spec = Spec
            .Build(underlyingSpec)
            .AsAtLeastNSatisfied(1)
            .WhenTrue("At least one satisfied")
            .WhenFalse("None satisfied")
            .Create();
            
        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Justification;
        
        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                            none satisfied
                                                !is true
                                                !is true
                                                !is true
                                            """)]
    [InlineAutoData(false, false, true,  """
                                            at least one satisfied
                                                is true
                                            """)]
    [InlineAutoData(false, true, false,  """
                                            at least one satisfied
                                                is true
                                            """)]
    [InlineAutoData(false, true, true,   """
                                            at least one satisfied
                                                is true
                                                is true
                                            """)]
    [InlineAutoData(true, false, false,  """
                                            at least one satisfied
                                                is true
                                            """)]
    [InlineAutoData(true, false, true,   """
                                            at least one satisfied
                                                is true
                                                is true
                                            """)]
    [InlineAutoData(true, true, false,   """
                                            at least one satisfied
                                                is true
                                                is true
                                            """)]
    [InlineAutoData(true, true, true,    """
                                            at least one satisfied
                                                is true
                                                is true
                                                is true
                                            """)]
    public void Should_serialize_the_result_of_the_at_least_n_satisified_operation(
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
            .AsAtLeastNSatisfied(1)
            .WhenTrue("at least one satisfied")
            .WhenFalse("none satisfied")
            .Create();
        
        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Justification;
        
        // Assert
        act.Should().Be(expected);
    }

    [Fact]
    public void Should_provide_a_proposition_statement_for_the_specification()
    {
        // Arrange
        const string expected = "at least one satisfied";
        
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("underlying spec description");

        var spec = Spec
            .Build(underlyingSpec)
            .AsAtLeastNSatisfied(1)
            .WhenTrue("at least one satisfied")
            .WhenFalse("none satisfied")
            .Create();

        // Act
        var act = spec.Statement;
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Fact]
    public void Should_serialize_the_spec_using_the_ToString_method()
    {
        // Arrange
        const string expected = "at least one satisfied";
        
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("underlying spec description");

        var spec = Spec
            .Build(underlyingSpec)
            .AsAtLeastNSatisfied(1)
            .WhenTrue("at least one satisfied")
            .WhenFalse("none satisfied")
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
            .AsAtLeastNSatisfied(2)
            .Create("all are true");

        var result = spec.IsSatisfiedBy([firstModel, secondModel, thirdModel]);

        // Act
        var act = result.Description.CausalOperandCount;
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, true)]
    public void Should_perform_an_at_least_n_satisfied_operation_when_using_a_boolean_predicate_function(
        bool first,
        bool second,
        bool third,
        bool expected)
    {
        // Arrange
        var spec =
            Spec.Build((bool m) => m)
                .AsAtLeastNSatisfied(2)
                .WhenTrue(_ => "at least 2 true")
                .WhenFalse(_ => "!at least 2 true")
                .Create("at least 2 true");

        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }
    
    
    [Theory]
    [InlineData(false, false, false, "!at least 2 true")]
    [InlineData(false, false, true, "!at least 2 true")]
    [InlineData(false, true, false, "!at least 2 true")]
    [InlineData(false, true, true, "at least 2 true")]
    [InlineData(true, false, false, "!at least 2 true")]
    [InlineData(true, false, true, "at least 2 true")]
    [InlineData(true, true, false, "at least 2 true")]
    [InlineData(true, true, true, "at least 2 true")]
    public void Should_supply_a_reason_when_using_a_boolean_predicate_function(
        bool first,
        bool second,
        bool third,
        string expectedReason)
    {
        // Arrange
        var spec =
            Spec.Build((bool m) => m)
                .AsAtLeastNSatisfied(2)
                .WhenTrue(_ => "at least 2 true")
                .WhenFalse(_ => "!at least 2 true")
                .Create("at least 2 true");

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
    [InlineData(true, true, true, true)]
    public void Should_perform_an_at_least_n_satisfied_operation_when_using_a_boolean_result_predicate_function(
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
                .AsAtLeastNSatisfied(2)
                .WhenTrue(_ => "at least 2 true")
                .WhenFalse(_ => "!at least 2 true")
                .Create("at least 2 true");

        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Satisfied;
        
        // Assert
        act.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(false, false, false, "!at least 2 true")]
    [InlineData(false, false, true, "!at least 2 true")]
    [InlineData(false, true, false, "!at least 2 true")]
    [InlineData(false, true, true, "at least 2 true")]
    [InlineData(true, false, false, "!at least 2 true")]
    [InlineData(true, false, true, "at least 2 true")]
    [InlineData(true, true, false, "at least 2 true")]
    [InlineData(true, true, true, "at least 2 true")]
    public void Should_supply_a_reason_when_using_a_boolean_result_predicate_function(
        bool first,
        bool second,
        bool third,
        string expectedReason)
    {
        // Arrange
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is true");

        // Assert
        var spec =
            Spec.Build((bool model) => underlying.IsSatisfiedBy(model))
                .AsAtLeastNSatisfied(2)
                .WhenTrue(_ => "at least 2 true")
                .WhenFalse(_ => "!at least 2 true")
                .Create("at least 2 true");

        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }
}