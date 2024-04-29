﻿using FluentAssertions;

namespace Motiv.Tests;

public class AllSatisfiedSpecTests
{
    [Theory]
    [InlineAutoData(false, false, false, false)]
    [InlineAutoData(false, false, true, false)]
    [InlineAutoData(false, true, false, false)]
    [InlineAutoData(false, true, true, false)]
    [InlineAutoData(true, false, false, false)]
    [InlineAutoData(true, false, true, false)]
    [InlineAutoData(true, true, false, false)]
    [InlineAutoData(true, true, true, true)]
    public void Should_perform_the_logical_operation_All(
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
            .AsAllSatisfied()
            .Create("all are true");

        var result = sut.IsSatisfiedBy(models);

        result.Satisfied.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                            !all are true
                                                false
                                                false
                                                false
                                            """)]
    [InlineAutoData(false, false, true, """
                                            !all are true
                                                false
                                                false
                                            """)]
    [InlineAutoData(false, true, false, """
                                            !all are true
                                                false
                                                false
                                            """)]
    [InlineAutoData(false, true, true, """
                                            !all are true
                                                false
                                            """)]
    [InlineAutoData(true, false, false, """
                                            !all are true
                                                false
                                                false
                                            """)]
    [InlineAutoData(true, false, true, """
                                            !all are true
                                                false
                                            """)]
    [InlineAutoData(true, true, false, """
                                            !all are true
                                                false
                                            """)]
    [InlineAutoData(true, true, true, """
                                            all are true
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
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString().ToLowerInvariant())
            .WhenFalse(false.ToString().ToLowerInvariant())
            .Create();

        var sut = Spec
            .Build(underlyingSpec)
            .AsAllSatisfied()
            .WhenTrue(evaluation => evaluation.Metadata)
            .WhenFalse(evaluation => evaluation.Metadata)
            .Create("all are true");

        var result = sut.IsSatisfiedBy([first, second, third]);

        result.Justification.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                        !all are true
                                            false
                                            false
                                            false
                                        """)]
    [InlineAutoData(false, false, true, """
                                        !all are true
                                            false
                                            false
                                        """)]
    [InlineAutoData(false, true, false, """
                                        !all are true
                                            false
                                            false
                                        """)]
    [InlineAutoData(false, true, true, """
                                        !all are true
                                            false
                                        """)]
    [InlineAutoData(true, false, false, """
                                        !all are true
                                            false
                                            false
                                        """)]
    [InlineAutoData(true, false, true, """
                                        !all are true
                                            false
                                        """)]
    [InlineAutoData(true, true, false, """
                                        !all are true
                                            false
                                        """)]
    [InlineAutoData(true, true, true, """
                                        all are true
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
            .AsAllSatisfied()
            .WhenTrue(evaluation => evaluation.Metadata)
            .WhenFalse(evaluation => evaluation.Metadata)
            .Create("all are true");


        var result = sut.IsSatisfiedBy([first, second, third]);

        result.Justification.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                        !all are true
                                            !is true
                                            !is true
                                            !is true
                                        """)]
    [InlineAutoData(false, false, true, """
                                        !all are true
                                            !is true
                                            !is true
                                        """)]
    [InlineAutoData(false, true, false, """
                                        !all are true
                                            !is true
                                            !is true
                                        """)]
    [InlineAutoData(false, true, true, """
                                        !all are true
                                            !is true
                                        """)]
    [InlineAutoData(true, false, false, """
                                        !all are true
                                            !is true
                                            !is true
                                        """)]
    [InlineAutoData(true, false, true, """
                                       !all are true
                                           !is true
                                       """)]
    [InlineAutoData(true, true, false, """
                                       !all are true
                                           !is true
                                       """)]
    [InlineAutoData(true, true, true, """
                                      all are true
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
            .AsAllSatisfied()
            .WhenTrue(evaluation => evaluation.Metadata)
            .WhenFalse(evaluation => evaluation.Metadata)
            .Create("all are true");

        var result = sut.IsSatisfiedBy([first, second, third]);

        result.Justification.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                            !all are true
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
                                            !all are true
                                                AND
                                                    !left
                                                    !right
                                                AND
                                                    !left
                                                    !right
                                            """)]
    [InlineAutoData(false, true, false, """
                                            !all are true
                                                AND
                                                    !left
                                                    !right
                                                AND
                                                    !left
                                                    !right
                                            """)]
    [InlineAutoData(false, true, true, """
                                            !all are true
                                                AND
                                                    !left
                                                    !right
                                            """)]
    [InlineAutoData(true, false, false, """
                                            !all are true
                                                AND
                                                    !left
                                                    !right
                                                AND
                                                    !left
                                                    !right
                                            """)]
    [InlineAutoData(true, false, true, """
                                            !all are true
                                                AND
                                                    !left
                                                    !right
                                            """)]
    [InlineAutoData(true, true, false, """
                                            !all are true
                                                AND
                                                    !left
                                                    !right
                                            """)]
    [InlineAutoData(true, true, true, """
                                            all are true
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
        var left = Spec
            .Build((bool m) => m)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("left");

        var right = Spec
            .Build((bool m) => m)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("right");

        var sut = Spec
            .Build(left & right)
            .AsAllSatisfied()
            .WhenTrue(evaluation => evaluation.Metadata)
            .WhenFalse(evaluation => evaluation.Metadata)
            .Create("all are true");

        bool[] models = [first, second, third];
        var result = sut.IsSatisfiedBy(models);

        result.Justification.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "!all are true")]
    [InlineAutoData(false, false, true, "!all are true")]
    [InlineAutoData(false, true, false, "!all are true")]
    [InlineAutoData(false, true, true, "!all are true")]
    [InlineAutoData(true, false, false, "!all are true")]
    [InlineAutoData(true, false, true, "!all are true")]
    [InlineAutoData(true, true, false, "!all are true")]
    [InlineAutoData(true, true, true, "all are true")]
    public void Should_Describe_the_result_of_the_all_operation_and_show_multiple_underlying_causes(
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
            .AsAllSatisfied()
            .WhenTrue(evaluation => evaluation.Metadata)
            .WhenFalse(evaluation => evaluation.Metadata)
            .Create("all are true");

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

        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create("is true");

        var sut = Spec
            .Build(underlyingSpec)
            .AsAllSatisfied()
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
        const string expectedSummary = "all are true";
        const string expectedFull =
            """
            all are true
                is true
            """;

        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .Create();

        var sut = Spec
            .Build(underlyingSpec)
            .AsAllSatisfied()
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("all are true");

        sut.Statement.Should().Be(expectedSummary);
        sut.Expression.Should().Be(expectedFull);
        sut.ToString().Should().Be(expectedSummary);
    }
    
    [Theory]
    [InlineAutoData(false, false, false, 3)]
    [InlineAutoData(false, false, true, 2)]
    [InlineAutoData(false, true, false, 2)]
    [InlineAutoData(false, true, true, 1)]
    [InlineAutoData(true, false, false, 2)]
    [InlineAutoData(true, false, true, 1)]
    [InlineAutoData(true, true, false, 1)]
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
            .AsAllSatisfied()
            .Create("all are true");

        var result = sut.IsSatisfiedBy([firstModel, secondModel, thirdModel]);

        result.Description.CausalOperandCount.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "!all are true")]
    [InlineAutoData(false, true, false, "!all are true")]
    [InlineAutoData(true, false, false, "!all are true")]
    [InlineAutoData(true, true, true, "all are true")]
    public void Should_surface_boolean_results_created_from_underlyingResult(bool modelA, bool modelB, bool expected, string expectedAssertion)
    {
        var underlying = Spec
            .Build((bool m) => m)
            .Create("underlying");

        var sut = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .AsAllSatisfied()
            .Create("all are true");
        
        var act = sut.IsSatisfiedBy([modelA, modelB]);
        
        act.Satisfied.Should().Be(expected);
        act.Reason.Should().Be(expectedAssertion);
    }

    [Theory]
    [InlineAutoData(false, false, false, "not all are true")]
    [InlineAutoData(false, true, false, "not all are true")]
    [InlineAutoData(true, false, false, "not all are true")]
    [InlineAutoData(true, true, true, "all are true")]
    public void Should_surface_boolean_results_with_custom_assertions_created_from_underlyingResult(bool modelA, bool modelB, bool expected, string expectedAssertion)
    {
        var underlying = Spec
            .Build((bool m) => m)
            .Create("underlying");

        var sut = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .AsAllSatisfied()
            .WhenTrue("all are true")
            .WhenFalse("not all are true")
            .Create();
        
        var act = sut.IsSatisfiedBy([modelA, modelB]);
        
        act.Satisfied.Should().Be(expected);
        act.Reason.Should().Be(expectedAssertion);
        act.Assertions.Should().BeEquivalentTo([expectedAssertion]);
    }
    
    [Theory]
    [InlineAutoData(false, false, false, "not all are true")]
    [InlineAutoData(false, true, false, "not all are true")]
    [InlineAutoData(true, false, false, "not all are true")]
    [InlineAutoData(true, true, true, "all are true")]
    public void Should_surface_boolean_results_created_from_predicate(bool modelA, bool modelB, bool expected, string expectedAssertion)
    {
        var sut = Spec
            .Build((bool m) => m)
            .AsAllSatisfied()
            .WhenTrue("all are true")
            .WhenFalse("not all are true")
            .Create();
        
        var act = sut.IsSatisfiedBy([modelA, modelB]);
        
        act.Satisfied.Should().Be(expected);
        act.Reason.Should().Be(expectedAssertion);
        act.Assertions.Should().BeEquivalentTo([expectedAssertion]);
    }
    
    [Theory]
    [InlineData(false, false, false, "not all are true")]
    [InlineData(false, true, false, "not all are true")]
    [InlineData(true, false, false, "not all are true")]
    [InlineData(true, true, true, "all are true")]
    public void Should_surface_boolean_results_created_from_predicate_when_a_proposition_is_specified(
        bool modelA,
        bool modelB,
        bool expected,
        string expectedReason)
    {
        var sut = Spec
            .Build((bool m) => m)
            .AsAllSatisfied()
            .WhenTrue("all are true")
            .WhenFalse("not all are true")
            .Create("all true");
        
        var act = sut.IsSatisfiedBy([modelA, modelB]);
        
        act.Satisfied.Should().Be(expected);
        act.Reason.Should().Be(expectedReason);
        act.Assertions.Should().BeEquivalentTo([expectedReason]);
    }
}