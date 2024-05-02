﻿using FluentAssertions;

namespace Motiv.Tests;

public class AsNoneSatisfiedSpecTests
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
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        bool[] models = [first, second, third];

        var sut = Spec
            .Build(underlyingSpec)
            .AsNoneSatisfied()
            .Create("none are true");

        var result = sut.IsSatisfiedBy(models);

        result.Satisfied.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                         none are true
                                             false
                                             false
                                             false
                                         """)]
    [InlineAutoData(false, false, true, """
                                        !none are true
                                            true
                                        """)]
    [InlineAutoData(false, true, false, """
                                        !none are true
                                            true
                                        """)]
    [InlineAutoData(false, true, true, """
                                        !none are true
                                            true
                                            true
                                        """)]
    [InlineAutoData(true, false, false, """
                                        !none are true
                                            true
                                        """)]
    [InlineAutoData(true, false, true, """
                                        !none are true
                                            true
                                            true
                                        """)]
    [InlineAutoData(true, true, false, """
                                        !none are true
                                            true
                                            true
                                        """)]
    [InlineAutoData(true, true, true, """
                                        !none are true
                                            true
                                            true
                                            true
                                        """)]
    public void Should_serialize_the_result_of_the_all_operation_when_metadata_is_a_string(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpec =
            Spec.Build((bool m) => m)
                .WhenTrue(true.ToString().ToLowerInvariant())
                .WhenFalse(false.ToString().ToLowerInvariant())
                .Create();

        var sut =
            Spec.Build(underlyingSpec)
                .AsNoneSatisfied()
                .WhenTrue(evaluation => evaluation.Metadata)
                .WhenFalse(evaluation => evaluation.Metadata)
                .Create("none are true");

        var result = sut.IsSatisfiedBy([first, second, third]);

        result.Justification.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                        none are true
                                            false
                                            false
                                            false
                                        """)]
    [InlineAutoData(false, false, true, """
                                        !none are true
                                            true
                                        """)]
    [InlineAutoData(false, true, false, """
                                        !none are true
                                            true
                                        """)]
    [InlineAutoData(false, true, true, """
                                        !none are true
                                            true
                                            true
                                        """)]
    [InlineAutoData(true, false, false, """
                                        !none are true
                                            true
                                        """)]
    [InlineAutoData(true, false, true, """
                                        !none are true
                                            true
                                            true
                                        """)]
    [InlineAutoData(true, true, false, """
                                        !none are true
                                            true
                                            true
                                        """)]
    [InlineAutoData(true, true, true, """
                                        !none are true
                                            true
                                            true
                                            true
                                        """)]
    public void
        Should_serialize_the_result_of_the_all_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
            bool first,
            bool second,
            bool third,
            string expected)
    {
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString().ToLowerInvariant())
            .WhenFalse(false.ToString().ToLowerInvariant())
            .Create();

        var sut = Spec
            .Build(underlyingSpec)
            .AsNoneSatisfied()
            .WhenTrue(evaluation => evaluation.Metadata)
            .WhenFalse(evaluation => evaluation.Metadata)
            .Create("none are true");


        var result = sut.IsSatisfiedBy([first, second, third]);

        result.Justification.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                        none are true
                                            !is true
                                            !is true
                                            !is true
                                        """)]
    [InlineAutoData(false, false, true, """
                                        !none are true
                                            is true
                                        """)]
    [InlineAutoData(false, true, false, """
                                        !none are true
                                            is true
                                        """)]
    [InlineAutoData(false, true, true, """
                                        !none are true
                                            is true
                                            is true
                                        """)]
    [InlineAutoData(true, false, false, """
                                        !none are true
                                            is true
                                        """)]
    [InlineAutoData(true, false, true, """
                                       !none are true
                                           is true
                                           is true
                                       """)]
    [InlineAutoData(true, true, false, """
                                       !none are true
                                           is true
                                           is true
                                       """)]
    [InlineAutoData(true, true, true, """
                                      !none are true
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
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("is true");

        var sut = Spec
            .Build(underlyingSpec)
            .AsNoneSatisfied()
            .WhenTrue(evaluation => evaluation.Metadata)
            .WhenFalse(evaluation => evaluation.Metadata)
            .Create("none are true");

        var result = sut.IsSatisfiedBy([first, second, third]);

        result.Justification.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                        none are true
                                            AND
                                                !left
                                                !right
                                            AND
                                                !left
                                                !right
                                            AND
                                                !left
                                                !right
                                        """)]
    [InlineAutoData(false, false, true, """
                                        !none are true
                                            AND
                                                left
                                                right
                                        """)]
    [InlineAutoData(false, true, false, """
                                        !none are true
                                            AND
                                                left
                                                right
                                        """)]
    [InlineAutoData(false, true, true, """
                                        !none are true
                                            AND
                                                left
                                                right
                                            AND
                                                left
                                                right
                                        """)]
    [InlineAutoData(true, false, false, """
                                        !none are true
                                            AND
                                                left
                                                right
                                        """)]
    [InlineAutoData(true, false, true, """
                                        !none are true
                                            AND
                                                left
                                                right
                                            AND
                                                left
                                                right
                                        """)]
    [InlineAutoData(true, true, false, """
                                        !none are true
                                            AND
                                                left
                                                right
                                            AND
                                                left
                                                right
                                        """)]
    [InlineAutoData(true, true, true, """
                                        !none are true
                                            AND
                                                left
                                                right
                                            AND
                                                left
                                                right
                                            AND
                                                left
                                                right
                                        """)]
    public void Should_serialize_the_result_of_the_all_operation_and_show_multiple_underlying_causes(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        var underlyingSpecLeft = Spec
            .Build((bool m) => m)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("left");

        var underlyingSpecRight = Spec
            .Build((bool m) => m)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("right");

        var sut = Spec
            .Build(underlyingSpecLeft & underlyingSpecRight)
            .AsNoneSatisfied()
            .WhenTrue(evaluation => evaluation.Metadata)
            .WhenFalse(evaluation => evaluation.Metadata)
            .Create("none are true");

        bool[] models = [first, second, third];
        var result = sut.IsSatisfiedBy(models);

        result.Justification.Should().Be(expected);
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
        var underlyingSpecLeft =
            Spec.Build((bool m) => m)
                .WhenTrue(true)
                .WhenFalse(false)
                .Create("left");

        var underlyingSpecRight =
            Spec.Build((bool m) => m)
                .WhenTrue(true)
                .WhenFalse(false)
                .Create("right");

        var sut =
            Spec.Build(underlyingSpecLeft & underlyingSpecRight)
                .AsNoneSatisfied()
                .WhenTrue(evaluation => evaluation.Metadata)
                .WhenFalse(evaluation => evaluation.Metadata)
                .Create("none are true");

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
            all booleans are true
                is true
            """;

        var underlyingSpec =
            Spec.Build((bool m) => m)
                .WhenTrue(true.ToString())
                .WhenFalse(false.ToString())
                .Create("is true");

        var sut =
            Spec.Build(underlyingSpec)
                .AsNoneSatisfied()
                .WhenTrue("all  true")
                .WhenFalse(evaluation => $"{evaluation.FalseCount} false")
                .Create("all booleans are true");

        sut.Statement.Should().Be(expectedSummary);
        sut.Expression.Should().Be(expectedFull);
        sut.ToString().Should().Be(expectedSummary);
    }

    [Fact]  
    public void Should_provide_a_description_of_the_specification_when_metadata_is_a_string()
    {
        const string expectedSummary = "none are true";
        const string expectedFull =
            """
            none are true
                is true
            """;

        var underlyingSpec =
            Spec.Build((bool m) => m)
                .WhenTrue("is true")
                .WhenFalse("is false")
                .Create();

        var sut =
            Spec.Build(underlyingSpec)
                .AsNoneSatisfied()
                .WhenTrue(true)
                .WhenFalse(false)
                .Create("none are true");

        sut.Statement.Should().Be(expectedSummary);
        sut.Expression.Should().Be(expectedFull);
        sut.ToString().Should().Be(expectedSummary);
    }
    
    [Theory]
    [InlineData(false, false, false, true, "none are true")]
    [InlineData(false, false, true, false, "!none are true")]
    [InlineData(false, true, false, false, "!none are true")]
    [InlineData(false, true, true, false, "!none are true")]
    [InlineData(true, false, false, false, "!none are true")]
    [InlineData(true, false, true, false, "!none are true")]
    [InlineData(true, true, false, false, "!none are true")]
    [InlineData(true, true, true, false, "!none are true")]
    public void Should_perform_a_none_satisfied_operation_when_using_a_boolean_predicate_function(
        bool first,
        bool second,
        bool third,
        bool expected,
        string expectedReason)
    {

        bool[] models = [first, second, third];

        var sut =
            Spec.Build((bool m) => m)
                .AsNoneSatisfied()
                .WhenTrue(_ => "none are true")
                .WhenFalse(_ => "!none are true")
                .Create("none are true");

        var result = sut.IsSatisfiedBy(models);

        result.Satisfied.Should().Be(expected);
        result.Reason.Should().Be(expectedReason);
    }
    
    [Theory]
    [InlineData(false, false, false, true, "none are true")]
    [InlineData(false, false, true, false, "!none are true")]
    [InlineData(false, true, false, false, "!none are true")]
    [InlineData(false, true, true, false, "!none are true")]
    [InlineData(true, false, false, false, "!none are true")]
    [InlineData(true, false, true, false, "!none are true")]
    [InlineData(true, true, false, false, "!none are true")]
    [InlineData(true, true, true, false, "!none are true")]
    public void Should_perform_a_none_satisfied_operation_when_using_a_boolean_result_predicate_function(
        bool first,
        bool second,
        bool third,
        bool expected,
        string expectedReason)
    {
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is true");

        bool[] models = [first, second, third];

        var sut =
            Spec.Build((bool model) => underlying.IsSatisfiedBy(model))
                .AsNoneSatisfied()
                .WhenTrue(_ => "none are true")
                .WhenFalse(_ => "!none are true")
                .Create("none are true");

        var result = sut.IsSatisfiedBy(models);

        result.Satisfied.Should().Be(expected);
        result.Reason.Should().Be(expectedReason);
    }
}