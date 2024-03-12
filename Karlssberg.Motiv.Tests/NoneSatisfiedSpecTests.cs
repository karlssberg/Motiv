using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class NoneSatisfiedSpecTests
{
    [Theory]
    [InlineAutoData(false, false, false, true)]
    [InlineAutoData(false, false, true, false)]
    [InlineAutoData(false, true, false, false)]
    [InlineAutoData(false, true, true, false)]
    [InlineAutoData(true, false, false, false)]
    [InlineAutoData(true, false, true, false)]
    [InlineAutoData(true, true, false, false)]
    [InlineAutoData(true, true, true, false)]
    public void Should_perform_a_none_satisfied_operation(
        bool first,
        bool second,
        bool third,
        bool expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .CreateSpec();

        bool[] models = [first, second, third];

        var sut = Spec
            .Build(underlyingSpec)
            .AsNoneSatisfied()
            .CreateSpec("none are true");

        var result = sut.IsSatisfiedBy(models);

        result.Satisfied.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                         none are true
                                         """)]
    [InlineAutoData(false, false, true, """
                                        !none are true {
                                            1x true
                                        }
                                        """)]
    [InlineAutoData(false, true, false, """
                                        !none are true {
                                            1x true
                                        }
                                        """)]
    [InlineAutoData(false, true, true, """
                                       !none are true {
                                           2x true
                                       }
                                       """)]
    [InlineAutoData(true, false, false, """
                                        !none are true {
                                            1x true
                                        }
                                        """)]
    [InlineAutoData(true, false, true, """
                                       !none are true {
                                           2x true
                                       }
                                       """)]
    [InlineAutoData(true, true, false, """
                                       !none are true {
                                           2x true
                                       }
                                       """)]
    [InlineAutoData(true, true, true, """
                                      !none are true {
                                          3x true
                                      }
                                      """)]
    public void Should_serialize_the_result_of_the_all_operation_when_metadata_is_a_string(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue(true.ToString().ToLowerInvariant())
            .WhenFalse(false.ToString().ToLowerInvariant())
            .CreateSpec();

        var sut = Spec
            .Build(underlyingSpec)
            .AsNoneSatisfied()
            .WhenTrue(evaluation => evaluation.Metadata)
            .WhenFalse(evaluation => evaluation.Metadata)
            .CreateSpec("none are true");

        var result = sut.IsSatisfiedBy([first, second, third]);

        result.Description.Detailed.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                         none are true
                                         """)]
    [InlineAutoData(false, false, true, """
                                        !none are true {
                                            1x true
                                        }
                                        """)]
    [InlineAutoData(false, true, false, """
                                        !none are true {
                                            1x true
                                        }
                                        """)]
    [InlineAutoData(false, true, true, """
                                       !none are true {
                                           2x true
                                       }
                                       """)]
    [InlineAutoData(true, false, false, """
                                        !none are true {
                                            1x true
                                        }
                                        """)]
    [InlineAutoData(true, false, true, """
                                       !none are true {
                                           2x true
                                       }
                                       """)]
    [InlineAutoData(true, true, false, """
                                       !none are true {
                                           2x true
                                       }
                                       """)]
    [InlineAutoData(true, true, true, """
                                      !none are true {
                                          3x true
                                      }
                                      """)]
    public void
        Should_serialize_the_result_of_the_all_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
            bool first,
            bool second,
            bool third,
            string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue(true.ToString().ToLowerInvariant())
            .WhenFalse(false.ToString().ToLowerInvariant())
            .CreateSpec();

        var sut = Spec
            .Build(underlyingSpec)
            .AsNoneSatisfied()
            .WhenTrue(evaluation => evaluation.Metadata)
            .WhenFalse(evaluation => evaluation.Metadata)
            .CreateSpec("none are true");


        var result = sut.IsSatisfiedBy([first, second, third]);

        result.Description.Detailed.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                         none are true
                                         """)]
    [InlineAutoData(false, false, true, """
                                        !none are true {
                                            1x is true
                                        }
                                        """)]
    [InlineAutoData(false, true, false, """
                                        !none are true {
                                            1x is true
                                        }
                                        """)]
    [InlineAutoData(false, true, true, """
                                       !none are true {
                                           2x is true
                                       }
                                       """)]
    [InlineAutoData(true, false, false, """
                                        !none are true {
                                            1x is true
                                        }
                                        """)]
    [InlineAutoData(true, false, true, """
                                       !none are true {
                                           2x is true
                                       }
                                       """)]
    [InlineAutoData(true, true, false, """
                                       !none are true {
                                           2x is true
                                       }
                                       """)]
    [InlineAutoData(true, true, true, """
                                      !none are true {
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
            .Build<bool>(m => m)
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("is true");

        var sut = Spec
            .Build(underlyingSpec)
            .AsNoneSatisfied()
            .WhenTrue(evaluation => evaluation.Metadata)
            .WhenFalse(evaluation => evaluation.Metadata)
            .CreateSpec("none are true");

        var result = sut.IsSatisfiedBy([first, second, third]);

        result.Description.Detailed.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                         none are true
                                         """)]
    [InlineAutoData(false, false, true, """
                                        !none are true {
                                            1x left & right
                                        }
                                        """)]
    [InlineAutoData(false, true, false, """
                                        !none are true {
                                            1x left & right
                                        }
                                        """)]
    [InlineAutoData(false, true, true, """
                                       !none are true {
                                           2x left & right
                                       }
                                       """)]
    [InlineAutoData(true, false, false, """
                                        !none are true {
                                            1x left & right
                                        }
                                        """)]
    [InlineAutoData(true, false, true, """
                                       !none are true {
                                           2x left & right
                                       }
                                       """)]
    [InlineAutoData(true, true, false, """
                                       !none are true {
                                           2x left & right
                                       }
                                       """)]
    [InlineAutoData(true, true, true, """
                                      !none are true {
                                          3x left & right
                                      }
                                      """)]
    public void Should_serialize_the_result_of_the_all_operation_and_show_multiple_underlying_causes(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpecLeft = Spec
            .Build<bool>(m => m)
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("left");

        var underlyingSpecRight = Spec
            .Build<bool>(m => m)
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("right");

        var sut = Spec
            .Build(underlyingSpecLeft & underlyingSpecRight)
            .AsNoneSatisfied()
            .WhenTrue(evaluation => evaluation.Metadata)
            .WhenFalse(evaluation => evaluation.Metadata)
            .CreateSpec("none are true");

        bool[] models = [first, second, third];
        var result = sut.IsSatisfiedBy(models);

        result.Description.Detailed.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "none are true")]
    [InlineAutoData(false, false, true, "!none are true")]
    [InlineAutoData(false, true, false, "!none are true")]
    [InlineAutoData(false, true, true, "!none are true")]
    [InlineAutoData(true, false, false, "!none are true")]
    [InlineAutoData(true, false, true, "!none are true")]
    [InlineAutoData(true, true, false, "!none are true")]
    [InlineAutoData(true, true, true, "!none are true")]
    public void Should_Describe_the_result_of_the_all_operation_and_show_multiple_underlying_causes(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpecLeft = Spec
            .Build<bool>(m => m)
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("left");

        var underlyingSpecRight = Spec
            .Build<bool>(m => m)
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("right");

        var sut = Spec
            .Build(underlyingSpecLeft & underlyingSpecRight)
            .AsNoneSatisfied()
            .WhenTrue(evaluation => evaluation.Metadata)
            .WhenFalse(evaluation => evaluation.Metadata)
            .CreateSpec("none are true");

        bool[] models = [first, second, third];
        var result = sut.IsSatisfiedBy(models);
        
        result.Reason.Should().Be(expected);
        result.ToString().Should().Be(expected);
    }


    [Fact]
    public void Should_provide_a_description_of_the_specification()
    {
        const string expectedSummary = "all booleans are true";
        const string expectedFull =
            """
            all booleans are true {
                is true
            }
            """;

        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .CreateSpec("is true");

        var sut = Spec
            .Build(underlyingSpec)
            .AsNoneSatisfied()
            .WhenTrue("all  true")
            .WhenFalse(evaluation => $"{evaluation.FalseCount} false")
            .CreateSpec("all booleans are true");

        sut.Proposition.Assertion.Should().Be(expectedSummary);
        sut.Proposition.Detailed.Should().Be(expectedFull);
        sut.ToString().Should().Be(expectedSummary);
    }

    [Fact]  
    public void Should_provide_a_description_of_the_specification_when_metadata_is_a_string()
    {
        const string expectedSummary = "none are true";
        const string expectedFull =
            """
            none are true {
                is true
            }
            """;

        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .CreateSpec();

        var sut = Spec
            .Build(underlyingSpec)
            .AsNoneSatisfied()
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("none are true");

        sut.Proposition.Assertion.Should().Be(expectedSummary);
        sut.Proposition.Detailed.Should().Be(expectedFull);
        sut.ToString().Should().Be(expectedSummary);
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
            .AsNoneSatisfied()
            .WhenTrue(evaluation => $"{evaluation.TrueCount} true")
            .WhenFalse(evaluation => $"{evaluation.FalseCount} false")
            .CreateSpec("all booleans are true");

        var act = () => sut.IsSatisfiedBy([model]);

        act.Should().Throw<SpecException>().Where(ex => ex.Message.Contains("ThrowingSpec<Object, String>"));
        act.Should().Throw<SpecException>().WithInnerExceptionExactly<Exception>()
            .Where(ex => ex.Message.Contains("should be wrapped"));
    }
}