using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class AtLeastNSatisfiedSpecBaseTests
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
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("returns the model");

        var sut = Spec
            .Build(underlyingSpec)
            .AsAtLeastNSatisfied(0)
            .WhenTrue("none satisfied")
            .WhenFalse("at least one satisfied")
            .Create();
        
        var result = sut.IsSatisfiedBy([first, second, third, fourth]);

        result.Satisfied.Should().Be(expected);
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
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("returns the model");

        var sut = Spec
            .Build(underlyingSpec)
            .AsAtLeastNSatisfied(1)
            .WhenTrue("One satisfied")
            .WhenFalse("None or more than one satisfied")
            .Create();
        
        var result = sut.IsSatisfiedBy([first, second, third, fourth]);

        result.Satisfied.Should().Be(expected);
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
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("returns the model");

        var sut = Spec
            .Build(underlyingSpec)
            .AsAtLeastNSatisfied(2)
            .WhenTrue("At least two satisfied")
            .WhenFalse("Less than two satisfied")
            .Create();
        
        var result = sut.IsSatisfiedBy([first, second, third, fourth]);

        result.Satisfied.Should().Be(expected);
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
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("returns the model");

        var sut = Spec
            .Build(underlyingSpec)
            .AsAtLeastNSatisfied(4)
            .WhenTrue("All satisfied")
            .WhenFalse("Not all satisfied")
            .Create();
        
        var result = sut.IsSatisfiedBy([first, second, third, fourth]);

        result.Satisfied.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                            none satisfied
                                            """)]
    [InlineAutoData(false, false, true,  """
                                            at least one satisfied {
                                                1x received true
                                            }
                                            """)]
    [InlineAutoData(false, true,  false, """
                                            at least one satisfied {
                                                1x received true
                                            }
                                            """)]
    [InlineAutoData(false, true,  true,  """
                                            at least one satisfied {
                                                2x received true
                                            }
                                            """)]
    [InlineAutoData(true,  false, false, """
                                            at least one satisfied {
                                                1x received true
                                            }
                                            """)]
    [InlineAutoData(true,  false, true,  """
                                            at least one satisfied {
                                                2x received true
                                            }
                                            """)]
    [InlineAutoData(true,  true,  false, """
                                            at least one satisfied {
                                                2x received true
                                            }
                                            """)]
    [InlineAutoData(true,  true,  true,  """
                                            at least one satisfied {
                                                3x received true
                                            }
                                            """)]
    public void Should_serialize_the_result_of_the_at_least_of_1_operation_when_metadata_is_a_string(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue("received true")
            .WhenFalse("received false")
            .Create();

        var sut = Spec
            .Build(underlyingSpec)
            .AsAtLeastNSatisfied(1)
            .WhenTrue("at least one satisfied")
            .WhenFalse("none satisfied")
            .Create();
            
        var result = sut.IsSatisfiedBy([first, second, third]);

        result.Description.Detailed.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                            None satisfied
                                            """)]
    [InlineAutoData(false, false, true,  """
                                            At least one satisfied {
                                                1x underlying satisfied
                                            }
                                            """)]
    [InlineAutoData(false, true, false,  """
                                            At least one satisfied {
                                                1x underlying satisfied
                                            }
                                            """)]
    [InlineAutoData(false, true, true,   """
                                            At least one satisfied {
                                                2x underlying satisfied
                                            }
                                            """)]
    [InlineAutoData(true, false, false,  """
                                            At least one satisfied {
                                                1x underlying satisfied
                                            }
                                            """)]
    [InlineAutoData(true, false, true,   """
                                            At least one satisfied {
                                                2x underlying satisfied
                                            }
                                            """)]
    [InlineAutoData(true, true, false,   """
                                            At least one satisfied {
                                                2x underlying satisfied
                                            }
                                            """)]
    [InlineAutoData(true, true, true,    """
                                            At least one satisfied {
                                                3x underlying satisfied
                                            }
                                            """)]
    public void Should_serialize_the_result_of_the_at_least_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue("underlying satisfied")
            .WhenFalse("underlying not satisfied")
            .Create();

        var sut = Spec
            .Build(underlyingSpec)
            .AsAtLeastNSatisfied(1)
            .WhenTrue("At least one satisfied")
            .WhenFalse("None satisfied")
            .Create();
            
        var result = sut.IsSatisfiedBy([first, second, third]);

        result.Description.Detailed.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                            none satisfied
                                            """)]
    [InlineAutoData(false, false, true,  """
                                            at least one satisfied {
                                                1x is true
                                            }
                                            """)]
    [InlineAutoData(false, true, false,  """
                                            at least one satisfied {
                                                1x is true
                                            }
                                            """)]
    [InlineAutoData(false, true, true,   """
                                            at least one satisfied {
                                                2x is true
                                            }
                                            """)]
    [InlineAutoData(true, false, false,  """
                                            at least one satisfied {
                                                1x is true
                                            }
                                            """)]
    [InlineAutoData(true, false, true,   """
                                            at least one satisfied {
                                                2x is true
                                            }
                                            """)]
    [InlineAutoData(true, true, false,   """
                                            at least one satisfied {
                                                2x is true
                                            }
                                            """)]
    [InlineAutoData(true, true, true,    """
                                            at least one satisfied {
                                                3x is true
                                            }
                                            """)]
    public void Should_serialize_the_result_of_the_at_least_n_satisified_operation(
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
            .AsAtLeastNSatisfied(1)
            .WhenTrue("at least one satisfied")
            .WhenFalse("none satisfied")
            .Create();
        
        var result = sut.IsSatisfiedBy([first, second, third]);

        result.Description.Detailed.Should().Be(expected);
    }

    [Fact]
    public void Should_provide_a_proposition_statement_for_the_specification()
    {
        const string expected = "at least one satisfied";
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("underlying spec description");

        var sut = Spec
            .Build(underlyingSpec)
            .AsAtLeastNSatisfied(1)
            .WhenTrue("at least one satisfied")
            .WhenFalse("none satisfied")
            .Create();

        sut.Proposition.Statement.Should().Be(expected);
        sut.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineAutoData]
    public void Should_wrap_thrown_exceptions_in_a_specification_exception(
        string model)
    {
        var throwingSpec = new ThrowingSpec<object, string>(
            "throws",
            new Exception("should be wrapped"));

        var sut = Spec
            .Build(throwingSpec)
            .AsAtLeastNSatisfied(1)
            .WhenTrue("At least one satisfied")
            .WhenFalse("None satisfied")
            .Create();

        var act = () => sut.IsSatisfiedBy([model]);

        act.Should().Throw<SpecException>().Where(ex => ex.Message.Contains("ThrowingSpec<Object, String>"));
        act.Should().Throw<SpecException>().WithInnerExceptionExactly<Exception>().Where(ex => ex.Message.Contains("should be wrapped"));
    }
    
    
    [Theory]
    [InlineAutoData(false, false, false, 0)]
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
            .AsAtLeastNSatisfied(2)
            .Create("all are true");

        var result = sut.IsSatisfiedBy([firstModel, secondModel, thirdModel]);

        result.Description.CausalOperandCount.Should().Be(expected);
    }
}