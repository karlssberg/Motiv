using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class AtMostNSatisfiedSpecTests
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
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("returns the model");

        var sut = Spec
            .Build(underlyingSpec)
            .AsAtMostNSatisfied(0)
            .WhenTrue("none are satisfied")
            .WhenFalse("one or more are satisfied")
            .Create();
        
        var result = sut.IsSatisfiedBy([first, second, third, fourth]);

        result.Satisfied.Should().Be(expected);
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
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("returns the model");

        var sut = Spec.Build(underlyingSpec)
            .AsAtMostNSatisfied(1)
            .WhenTrue("one is satisfied")
            .WhenFalse("none or more than one is not satisfied")
            .Create();
        
        var result = sut.IsSatisfiedBy([first, second, third, fourth]);

        result.Satisfied.Should().Be(expected);
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
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("returns the model");

        var sut = Spec.Build(underlyingSpec)
            .AsAtMostNSatisfied(2)
            .WhenTrue("at most two are satisfied")
            .WhenFalse("more than two are satisfied")
            .Create();
        
        var result = sut.IsSatisfiedBy([first, second, third, fourth]);

        result.Satisfied.Should().Be(expected);
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
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("returns the model");

        var sut = Spec
            .Build(underlyingSpec)
            .AsAtMostNSatisfied(4)
            .WhenTrue("at most four are satisfied")
            .WhenFalse("more than four are satisfied")
            .Create();
        
        var result = sut.IsSatisfiedBy([first, second, third, fourth]);

        result.Satisfied.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                            at most one is satisfied {
                                                3x is not satisfied
                                            }
                                            """)]
    [InlineAutoData(false, false, true,  """
                                            at most one is satisfied {
                                                1x is satisfied
                                            }
                                            """)]
    [InlineAutoData(false, true,  false, """
                                            at most one is satisfied {
                                                1x is satisfied
                                            }
                                            """)]
    [InlineAutoData(false, true,  true,  """
                                            more than one is satisfied {
                                                2x is satisfied
                                            }
                                            """)]
    [InlineAutoData(true,  false, false, """
                                            at most one is satisfied {
                                                1x is satisfied
                                            }
                                            """)]
    [InlineAutoData(true,  false, true,  """
                                            more than one is satisfied {
                                                2x is satisfied
                                            }
                                            """)]
    [InlineAutoData(true,  true,  false, """
                                            more than one is satisfied {
                                                2x is satisfied
                                            }
                                            """)]
    [InlineAutoData(true,  true,  true,  """
                                            more than one is satisfied {
                                                3x is satisfied
                                            }
                                            """)]
    public void Should_serialize_the_result_of_the_at_most_of_1_operation_when_metadata_is_a_string(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue("is satisfied")
            .WhenFalse("is not satisfied")
            .Create();;

        var sut = Spec
            .Build(underlyingSpec)
            .AsAtMostNSatisfied(1)
            .WhenTrue("at most one is satisfied")
            .WhenFalse("more than one is satisfied")
            .Create();
        
        var result = sut.IsSatisfiedBy([first, second, third]);

        result.Description.Detailed.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                            at most one is satisfied {
                                                3x False
                                            }
                                            """)]
    [InlineAutoData(false, false, true, """
                                            at most one is satisfied {
                                                1x True
                                            }
                                            """)]
    [InlineAutoData(false, true,  false, """
                                            at most one is satisfied {
                                                1x True
                                            }
                                            """)]
    [InlineAutoData(false, true,  true, """
                                            more than one is satisfied {
                                                2x True
                                            }
                                            """)]
    [InlineAutoData(true,  false, false, """
                                            at most one is satisfied {
                                                1x True
                                            }
                                            """)]
    [InlineAutoData(true,  false, true, """
                                            more than one is satisfied {
                                                2x True
                                            }
                                            """)]
    [InlineAutoData(true,  true,  false, """
                                            more than one is satisfied {
                                                2x True
                                            }
                                            """)]
    [InlineAutoData(true,  true,  true, """
                                            more than one is satisfied {
                                                3x True
                                            }
                                            """)]
    public void Should_serialize_the_result_of_the_at_most_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
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

        var sut = Spec
            .Build(underlyingSpec)
            .AsAtMostNSatisfied(1)
            .WhenTrue("at most one is satisfied")
            .WhenFalse("more than one is satisfied")
            .Create();
        
        var result = sut.IsSatisfiedBy([first, second, third]);

        result.Description.Detailed.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                            at most one is satisfied {
                                                3x !is true
                                            }
                                            """)]
    [InlineAutoData(false, false, true,  """
                                            at most one is satisfied {
                                                1x is true
                                            }
                                            """)]
    [InlineAutoData(false, true,  false, """
                                            at most one is satisfied {
                                                1x is true
                                            }
                                            """)]
    [InlineAutoData(false, true,  true,  """
                                            more than one is satisfied {
                                                2x is true
                                            }
                                            """)]
    [InlineAutoData(true,  false, false, """
                                            at most one is satisfied {
                                                1x is true
                                            }
                                            """)]
    [InlineAutoData(true,  false, true,  """
                                            more than one is satisfied {
                                                2x is true
                                            }
                                            """)]
    [InlineAutoData(true,  true,  false, """
                                            more than one is satisfied {
                                                2x is true
                                            }
                                            """)]
    [InlineAutoData(true,  true,  true,  """
                                            more than one is satisfied {
                                                3x is true
                                            }
                                            """)]
    public void Should_serialize_the_result_of_the_all_operation(
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
            .AsAtMostNSatisfied(1)
            .WhenTrue("at most one is satisfied")
            .WhenFalse("more than one is satisfied")
            .Create();
        
        var result = sut.IsSatisfiedBy([first, second, third]);

        result.Description.Detailed.Should().Be(expected);
    }

    [Fact]
    public void Should_provide_a_description_of_the_specification()
    {
        const string expected = "at most one is satisfied";
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue("underlying model is true")
            .WhenFalse("underlying model is false")
            .Create("underlying spec description");

        var sut = Spec
            .Build(underlyingSpec)
            .AsAtMostNSatisfied(1)
            .WhenTrue("at most one is satisfied")
            .WhenFalse("more than one is satisfied")
            .Create();

        sut.Description.Statement.Should().Be(expected);
        sut.ToString().Should().Be(expected);
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
            .AsAtMostNSatisfied(2)
            .Create("all are true");

        var result = sut.IsSatisfiedBy([firstModel, secondModel, thirdModel]);

        result.Description.CausalOperandCount.Should().Be(expected);
    }
}