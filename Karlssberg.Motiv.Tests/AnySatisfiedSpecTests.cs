using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class AnySatisfiedSpecTests
{
    [Theory]
    [InlineAutoData(false, false, false, false)]
    [InlineAutoData(false, false, true, true)]
    [InlineAutoData(false, true, false, true)]
    [InlineAutoData(false, true, true, true)]
    [InlineAutoData(true, false, false, true)]
    [InlineAutoData(true, false, true, true)]
    [InlineAutoData(true, true, false, true)]
    [InlineAutoData(true, true, true, true)]
    public void Should_perform_the_logical_operation_Any(
        bool first,
        bool second,
        bool third,
        bool expected)
    {
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        bool[] models = [first, second, third];

        var sut = Spec.Build(underlyingSpec)
            .AsAnySatisfied()
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("any satisfied");
        var result = sut.IsSatisfiedBy(models);

        result.Satisfied.Should().Be(expected);
    }
    
    [Fact]
    public void Should_provide_a_high_level_description_of_the_specification_when_metadata_is_a_string()
    {
        const string expected = "high-level description";
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue("boolean is true")   
            .WhenFalse("boolean is false")
            .Create();

        var sut = Spec
            .Build(underlyingSpec)
            .AsAnySatisfied()   
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("high-level description");

        sut.Statement.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                            !any satisfied
                                                !is true
                                                !is true
                                                !is true
                                            """)]
    [InlineAutoData(false, false, true,  """
                                            any satisfied
                                                is true
                                            """)]
    [InlineAutoData(false, true, false,  """
                                            any satisfied
                                                is true
                                            """)]
    [InlineAutoData(false, true, true,   """
                                            any satisfied
                                                is true
                                                is true
                                            """)]
    [InlineAutoData(true, false, false,  """
                                            any satisfied
                                                is true
                                            """)]
    [InlineAutoData(true, false, true,   """
                                            any satisfied
                                                is true
                                                is true
                                            """)]
    [InlineAutoData(true, true, false,   """
                                            any satisfied
                                                is true
                                                is true
                                            """)]
    [InlineAutoData(true, true, true,    """
                                            any satisfied
                                                is true
                                                is true
                                                is true
                                            """)]
    public void Should_serialize_the_result_of_the_any_operation(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("is true");

        var sut = Spec
            .Build(underlyingSpec)
            .AsAnySatisfied()
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("any satisfied");
            
        var result = sut.IsSatisfiedBy([first, second, third]);

        result.Justification.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                            some false
                                                False
                                                False
                                                False
                                            """)]
    [InlineAutoData(false, false, true,  """
                                            any true
                                                True
                                            """)]
    [InlineAutoData(false, true, false,  """
                                            any true
                                                True
                                            """)]
    [InlineAutoData(false, true, true,   """
                                            any true
                                                True
                                                True
                                            """)]
    [InlineAutoData(true, false, false,  """
                                            any true
                                                True
                                            """)]
    [InlineAutoData(true, false, true,   """
                                            any true
                                                True
                                                True
                                            """)]
    [InlineAutoData(true, true, false,   """
                                            any true
                                                True
                                                True
                                            """)]
    [InlineAutoData(true, true, true,    """
                                            any true
                                                True
                                                True
                                                True
                                            """)]
    public void Should_serialize_the_result_of_the_any_operation_when_metadata_is_a_string(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        bool[] models = [first, second, third];

        var sut = Spec
            .Build(underlyingSpec)
            .AsAnySatisfied()
            .WhenTrue("any true")
            .WhenFalse("some false")
            .Create();

        var result = sut.IsSatisfiedBy(models);

        result.Justification.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                            all false
                                                is false
                                                is false
                                                is false
                                            """)]
    [InlineAutoData(false, false, true,  """
                                            any true
                                                is true
                                            """)]
    [InlineAutoData(false, true, false,  """
                                            any true
                                                is true
                                            """)]
    [InlineAutoData(false, true, true,   """
                                            any true
                                                is true
                                                is true
                                            """)]
    [InlineAutoData(true, false, false,  """
                                            any true
                                                is true
                                            """)]
    [InlineAutoData(true, false, true,   """
                                            any true
                                                is true
                                                is true
                                            """)]
    [InlineAutoData(true, true, false,   """
                                            any true
                                                is true
                                                is true
                                            """)]
    [InlineAutoData(true, true, true,    """
                                            any true
                                                is true
                                                is true
                                                is true
                                            """)]
    public void Should_serialize_the_result_of_the_any_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .Create("is true");

        var sut = Spec
            .Build(underlyingSpec)
            .AsAnySatisfied()
            .WhenTrue(_ => "any true")
            .WhenFalse(_ => "all false")
            .Create("any true");
        
        var result = sut.IsSatisfiedBy([first, second, third]);

        result.Justification.Should().Be(expected);
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
        var underlying = Spec
            .Build((bool m) => m)
            .Create("underlying");
        
        var sut = Spec
            .Build(underlying)
            .AsAnySatisfied()
            .Create("any are true");

        var result = sut.IsSatisfiedBy([firstModel, secondModel, thirdModel]);

        result.Description.CausalOperandCount.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "!any true")]
    [InlineAutoData(false, true, true, "any true")]
    [InlineAutoData(true, false, true, "any true")]
    [InlineAutoData(true, true, true, "any true")]
    public void Should_surface_boolean_results_created_from_underlyingResult(bool modelA, bool modelB, bool expected, string expectedAssertion)
    {
        var underlying = Spec
            .Build((bool m) => m)
            .Create("underlying");

        var sut = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .AsAnySatisfied()
            .Create("any true");
        
        var act = sut.IsSatisfiedBy([modelA, modelB]);
        
        act.Satisfied.Should().Be(expected);
        act.Reason.Should().Be(expectedAssertion);
    }

    [Theory]
    [InlineAutoData(false, false, false, "none are true")]
    [InlineAutoData(false, true, true, "some are true")]
    [InlineAutoData(true, false, true, "some are true")]
    [InlineAutoData(true, true, true, "some are true")]
    public void Should_surface_boolean_results_with_custom_assertions_created_from_underlyingResult(bool modelA, bool modelB, bool expected, string expectedAssertion)
    {
        var underlying = Spec
            .Build((bool m) => m)
            .Create("underlying");

        var sut = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .AsAnySatisfied()
            .WhenTrue("some are true")
            .WhenFalse("none are true")
            .Create();
        
        var act = sut.IsSatisfiedBy([modelA, modelB]);
        
        act.Satisfied.Should().Be(expected);
        act.Reason.Should().Be(expectedAssertion);
        act.Assertions.Should().BeEquivalentTo([expectedAssertion]);
    }
    
    [Theory]
    [InlineAutoData(false, false, false, "none are true")]
    [InlineAutoData(false, true, true, "some are true")]
    [InlineAutoData(true, false, true, "some are true")]
    [InlineAutoData(true, true, true, "some are true")]
    public void Should_surface_boolean_results_created_from_predicate(bool modelA, bool modelB, bool expected, string expectedAssertion)
    {
        var sut = Spec
            .Build((bool m) => m)
            .AsAnySatisfied()
            .WhenTrue("some are true")
            .WhenFalse("none are true")
            .Create();
        
        var act = sut.IsSatisfiedBy([modelA, modelB]);
        
        act.Satisfied.Should().Be(expected);
        act.Reason.Should().Be(expectedAssertion);
        act.Assertions.Should().BeEquivalentTo([expectedAssertion]);
    }
    
    [Theory]
    [InlineData(false, false, false, "none are true")]
    [InlineData(false, true, true, "some are true")]
    [InlineData(true, false, true, "some are true")]
    [InlineData(true, true, true, "some are true")]
    public void Should_surface_boolean_results_created_from_predicate_when_a_proposition_is_specified(
        bool modelA,
        bool modelB,
        bool expected,
        string expectedReason)
    {
        var sut = Spec
            .Build((bool m) => m)
            .AsAnySatisfied()
            .WhenTrue("some are true")
            .WhenFalse("none are true")
            .Create("any true");
        
        var act = sut.IsSatisfiedBy([modelA, modelB]);
        
        act.Satisfied.Should().Be(expected);
        act.Reason.Should().Be(expectedReason);
        act.Assertions.Should().BeEquivalentTo([expectedReason]);
    }
}