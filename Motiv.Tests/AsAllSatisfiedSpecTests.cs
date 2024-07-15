namespace Motiv.Tests;

public class AsAllSatisfiedSpecTests
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
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        bool[] models = [first, second, third];

        var spec = Spec
            .Build(underlyingSpec)
            .AsAllSatisfied()
            .Create("all are true");

        // Act
        var act = spec.IsSatisfiedBy(models).Satisfied;

        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                            ¬all are true
                                                false
                                                false
                                                false
                                            """)]
    [InlineAutoData(false, false, true, """
                                            ¬all are true
                                                false
                                                false
                                            """)]
    [InlineAutoData(false, true, false, """
                                            ¬all are true
                                                false
                                                false
                                            """)]
    [InlineAutoData(false, true, true, """
                                            ¬all are true
                                                false
                                            """)]
    [InlineAutoData(true, false, false, """
                                            ¬all are true
                                                false
                                                false
                                            """)]
    [InlineAutoData(true, false, true, """
                                            ¬all are true
                                                false
                                            """)]
    [InlineAutoData(true, true, false, """
                                            ¬all are true
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
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString().ToLowerInvariant())
            .WhenFalse(false.ToString().ToLowerInvariant())
            .Create();

        var spec = Spec
            .Build(underlyingSpec)
            .AsAllSatisfied()
            .WhenTrueYield(evaluation => evaluation.Values)
            .WhenFalseYield(evaluation => evaluation.Values)
            .Create("all are true");

        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Justification;

        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                        ¬all are true
                                            false
                                            false
                                            false
                                        """)]
    [InlineAutoData(false, false, true, """
                                        ¬all are true
                                            false
                                            false
                                        """)]
    [InlineAutoData(false, true, false, """
                                        ¬all are true
                                            false
                                            false
                                        """)]
    [InlineAutoData(false, true, true, """
                                        ¬all are true
                                            false
                                        """)]
    [InlineAutoData(true, false, false, """
                                        ¬all are true
                                            false
                                            false
                                        """)]
    [InlineAutoData(true, false, true, """
                                        ¬all are true
                                            false
                                        """)]
    [InlineAutoData(true, true, false, """
                                        ¬all are true
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
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString().ToLowerInvariant())
            .WhenFalse(false.ToString().ToLowerInvariant())
            .Create();

        var spec = Spec
            .Build(underlyingSpec)
            .AsAllSatisfied()
            .WhenTrueYield(evaluation => evaluation.Values)
            .WhenFalseYield(evaluation => evaluation.Values)
            .Create("all are true");

        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Justification;

        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                        ¬all are true
                                            ¬is true
                                            ¬is true
                                            ¬is true
                                        """)]
    [InlineAutoData(false, false, true, """
                                        ¬all are true
                                            ¬is true
                                            ¬is true
                                        """)]
    [InlineAutoData(false, true, false, """
                                        ¬all are true
                                            ¬is true
                                            ¬is true
                                        """)]
    [InlineAutoData(false, true, true, """
                                        ¬all are true
                                            ¬is true
                                        """)]
    [InlineAutoData(true, false, false, """
                                        ¬all are true
                                            ¬is true
                                            ¬is true
                                        """)]
    [InlineAutoData(true, false, true, """
                                       ¬all are true
                                           ¬is true
                                       """)]
    [InlineAutoData(true, true, false, """
                                       ¬all are true
                                           ¬is true
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
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("is true");

        var spec = Spec
            .Build(underlyingSpec)
            .AsAllSatisfied()
            .WhenTrueYield(evaluation => evaluation.Values)
            .WhenFalseYield(evaluation => evaluation.Values)
            .Create("all are true");

        var result = spec.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Justification;

        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                            ¬all are true
                                                AND
                                                    ¬left
                                                    ¬right
                                                AND
                                                    ¬left
                                                    ¬right
                                                AND
                                                    ¬left
                                                    ¬right
                                            """)]
    [InlineAutoData(false, false, true, """
                                            ¬all are true
                                                AND
                                                    ¬left
                                                    ¬right
                                                AND
                                                    ¬left
                                                    ¬right
                                            """)]
    [InlineAutoData(false, true, false, """
                                            ¬all are true
                                                AND
                                                    ¬left
                                                    ¬right
                                                AND
                                                    ¬left
                                                    ¬right
                                            """)]
    [InlineAutoData(false, true, true, """
                                            ¬all are true
                                                AND
                                                    ¬left
                                                    ¬right
                                            """)]
    [InlineAutoData(true, false, false, """
                                            ¬all are true
                                                AND
                                                    ¬left
                                                    ¬right
                                                AND
                                                    ¬left
                                                    ¬right
                                            """)]
    [InlineAutoData(true, false, true, """
                                            ¬all are true
                                                AND
                                                    ¬left
                                                    ¬right
                                            """)]
    [InlineAutoData(true, true, false, """
                                            ¬all are true
                                                AND
                                                    ¬left
                                                    ¬right
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
        // Arrange
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
            .WhenTrueYield(evaluation => evaluation.Values)
            .WhenFalseYield(evaluation => evaluation.Values)
            .Create("all are true");

        var result = sut.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Justification;

        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "¬all are true")]
    [InlineAutoData(false, false, true, "¬all are true")]
    [InlineAutoData(false, true, false, "¬all are true")]
    [InlineAutoData(false, true, true, "¬all are true")]
    [InlineAutoData(true, false, false, "¬all are true")]
    [InlineAutoData(true, false, true, "¬all are true")]
    [InlineAutoData(true, true, false, "¬all are true")]
    [InlineAutoData(true, true, true, "all are true")]
    public void Should_Describe_the_result_of_the_all_operation_and_show_multiple_underlying_causes(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        // Arrange
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
            .WhenTrueYield(evaluation => evaluation.Values)
            .WhenFalseYield(evaluation => evaluation.Values)
            .Create("all are true");

        var result = sut.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "¬all are true")]
    [InlineAutoData(false, false, true, "¬all are true")]
    [InlineAutoData(false, true, false, "¬all are true")]
    [InlineAutoData(false, true, true, "¬all are true")]
    [InlineAutoData(true, false, false, "¬all are true")]
    [InlineAutoData(true, false, true, "¬all are true")]
    [InlineAutoData(true, true, false, "¬all are true")]
    [InlineAutoData(true, true, true, "all are true")]
    public void Should_serialize_the_result_of_the_all_operation_and_show_underlying_causes(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        // Arrange
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
            .WhenTrueYield(evaluation => evaluation.Values)
            .WhenFalseYield(evaluation => evaluation.Values)
            .Create("all are true");

        var result = sut.IsSatisfiedBy([first, second, third]);

        // Act
        var act = result.ToString();

        // Assert
        act.Should().Be(expected);
    }


    [Fact]
    public void Should_provide_a_statement_of_the_specification()
    {
        // Arrange
        const string expectedSummary = "all booleans are true";

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

        // Act
        var act = sut.Statement;

        // Assert
        act.Should().Be(expectedSummary);
    }

    [Fact]
    public void Should_provide_a_serialized_expression_tree_of_the_specification()
    {
        // Arrange
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

        var spec = Spec
            .Build(underlyingSpec)
            .AsAllSatisfied()
            .WhenTrue("all  true")
            .WhenFalse(evaluation => $"{evaluation.FalseCount} false")
            .Create("all booleans are true");

        // Act
        var act = spec.Expression;

        // Assert
        act.Should().Be(expectedFull);
    }

    [Fact]
    public void Should_provide_a_statement_of_the_specification_when_metadata_is_a_string()
    {
        // Arrange
        const string expectedSummary = "all are true";

        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .Create();

        var spec = Spec
            .Build(underlyingSpec)
            .AsAllSatisfied()
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("all are true");

        // Act
        var act = spec.Statement;

        // Assert
        act.Should().Be(expectedSummary);
    }

    [Fact]
    public void Should_provide_a_serialized_expression_tree_of_the_specification_when_metadata_is_a_string()
    {
        // Arrange
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
        var spec = Spec
            .Build(underlyingSpec)
            .AsAllSatisfied()
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("all are true");

        // Act
        var act = spec.Expression;

        // Assert
        act.Should().Be(expectedFull);
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
        // Arrange
        var underlying = Spec
            .Build((bool m) => m)
            .Create("underlying");

        var spec = Spec
            .Build(underlying)
            .AsAllSatisfied()
            .Create("all are true");

        var result = spec.IsSatisfiedBy([firstModel, secondModel, thirdModel]);

        // Act
        var act = result.Description.CausalOperandCount;

        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, false)]
    [InlineAutoData(true, false, false)]
    [InlineAutoData(true, true, true)]
    public void Should_surface_boolean_results_created_from_underlyingResult(bool modelA, bool modelB, bool expected)
    {
        // Arrange
        var underlying = Spec
            .Build((bool m) => m)
            .Create("underlying");

        var spec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .AsAllSatisfied()
            .Create("all are true");

        // Act
        var act = spec.IsSatisfiedBy([modelA, modelB]).Satisfied;

        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, "¬all are true")]
    [InlineAutoData(false, true, "¬all are true")]
    [InlineAutoData(true, false, "¬all are true")]
    [InlineAutoData(true, true, "all are true")]
    public void Should_surface_reasons_from_underlyingResult(bool modelA, bool modelB, string expectedAssertion)
    {
        // Arrange
        var underlying = Spec
            .Build((bool m) => m)
            .Create("underlying");

        var spec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .AsAllSatisfied()
            .Create("all are true");

        // Act
        var act = spec.IsSatisfiedBy([modelA, modelB]).Reason;

        // Assert
        act.Should().Be(expectedAssertion);
    }

    [Theory]
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, false)]
    [InlineAutoData(true, false, false)]
    [InlineAutoData(true, true, true)]
    public void Should_evaluate_boolean_results_created_with_custom_assertions(
        bool modelA,
        bool modelB,
        bool expected)
    {
        // Arrange
        var underlying = Spec
            .Build((bool m) => m)
            .Create("underlying");

        var spec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .AsAllSatisfied()
            .WhenTrue("all are true")
            .WhenFalse("not all are true")
            .Create();

        // Act
        var act = spec.IsSatisfiedBy([modelA, modelB]).Satisfied;

        // Assert
        act.Should().Be(expected);
    }


    [Theory]
    [InlineAutoData(false, false, "not all are true")]
    [InlineAutoData(false, true, "not all are true")]
    [InlineAutoData(true, false, "not all are true")]
    [InlineAutoData(true, true, "all are true")]
    public void Should_surface_assertions_from_boolean_results_created_with_custom_assertions(
        bool modelA,
        bool modelB,
        string expectedAssertion)
    {
        // Arrange
        var underlying = Spec
            .Build((bool m) => m)
            .Create("underlying");

        var spec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .AsAllSatisfied()
            .WhenTrue("all are true")
            .WhenFalse("not all are true")
            .Create();

        // Act
        var act = spec.IsSatisfiedBy([modelA, modelB]).Assertions;

        // Assert
        act.Should().BeEquivalentTo([expectedAssertion]);
    }


    [Theory]
    [InlineAutoData(false, false, "not all are true")]
    [InlineAutoData(false, true, "not all are true")]
    [InlineAutoData(true, false, "not all are true")]
    [InlineAutoData(true, true, "all are true")]
    public void Should_surface_reason_from_boolean_results_with_custom_assertions(
        bool modelA,
        bool modelB,
        string expectedAssertion)
    {
        // Arrange
        var underlying = Spec
            .Build((bool m) => m)
            .Create("underlying");

        var spec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .AsAllSatisfied()
            .WhenTrue("all are true")
            .WhenFalse("not all are true")
            .Create();

        // Act
        var act = spec.IsSatisfiedBy([modelA, modelB]).Reason;

        // Assert
        act.Should().Be(expectedAssertion);
    }

    [Theory]
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, false)]
    [InlineAutoData(true, false, false)]
    [InlineAutoData(true, true, true)]
    public void Should_evaluate_boolean_results_created_from_predicate(
        bool modelA,
        bool modelB,
        bool expected)
    {
        // Arrange
        var spec = Spec
            .Build((bool m) => m)
            .AsAllSatisfied()
            .WhenTrue("all are true")
            .WhenFalse("not all are true")
            .Create();

        var result = spec.IsSatisfiedBy([modelA, modelB]);

        // Act
        var act = result.Satisfied;

        // Assert
        act.Should().Be(expected);
    }


    [Theory]
    [InlineAutoData(false, false, "not all are true")]
    [InlineAutoData(false, true, "not all are true")]
    [InlineAutoData(true, false, "not all are true")]
    [InlineAutoData(true, true, "all are true")]
    public void Should_surface_reason_for_boolean_results_created_from_predicate(
        bool modelA,
        bool modelB,
        string expectedAssertion)
    {
        // Arrange
        var spec = Spec
            .Build((bool m) => m)
            .AsAllSatisfied()
            .WhenTrue("all are true")
            .WhenFalse("not all are true")
            .Create();

        var result = spec.IsSatisfiedBy([modelA, modelB]);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedAssertion);
    }

    [Theory]
    [InlineAutoData(false, false, "not all are true")]
    [InlineAutoData(false, true, "not all are true")]
    [InlineAutoData(true, false, "not all are true")]
    [InlineAutoData(true, true, "all are true")]
    public void Should_surface_assertion_of_boolean_results_created_from_predicate(
        bool modelA,
        bool modelB,
        string expectedAssertion)
    {
        // Arrange
        var spec = Spec
            .Build((bool m) => m)
            .AsAllSatisfied()
            .WhenTrue("all are true")
            .WhenFalse("not all are true")
            .Create();

        var result = spec.IsSatisfiedBy([modelA, modelB]);

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().BeEquivalentTo([expectedAssertion]);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void Should_evaluate_boolean_results_created_from_predicate_when_a_proposition_is_specified(
        bool modelA,
        bool modelB,
        bool expected)
    {
        // Arrange
        var spec = Spec
            .Build((bool m) => m)
            .AsAllSatisfied()
            .WhenTrue("all are true")
            .WhenFalse("not all are true")
            .Create("all true");

        var result = spec.IsSatisfiedBy([modelA, modelB]);

        // Act
        var act = result.Satisfied;

        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineData(false, false, "not all are true")]
    [InlineData(false, true, "not all are true")]
    [InlineData(true, false, "not all are true")]
    [InlineData(true, true, "all are true")]
    public void Should_surface_reason_for_boolean_results_created_from_predicate_when_a_proposition_is_specified(
        bool modelA,
        bool modelB,       string expectedReason)
    {
        // Arrange
        var spec = Spec
            .Build((bool m) => m)
            .AsAllSatisfied()
            .WhenTrue("all are true")
            .WhenFalse("not all are true")
            .Create("all true");

        var result = spec.IsSatisfiedBy([modelA, modelB]);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expectedReason);
    }

    [Theory]
    [InlineData(false, false, "not all are true")]
    [InlineData(false, true, "not all are true")]
    [InlineData(true, false, "not all are true")]
    [InlineData(true, true, "all are true")]
    public void Should_surface_assertions_of_boolean_results_created_from_predicate_when_a_proposition_is_specified(
        bool modelA,
        bool modelB,
        string expectedAssertion)
    {
        // Arrange
        var spec = Spec
            .Build((bool m) => m)
            .AsAllSatisfied()
            .WhenTrue("all are true")
            .WhenFalse("not all are true")
            .Create("all true");

        var result = spec.IsSatisfiedBy([modelA, modelB]);

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().BeEquivalentTo([expectedAssertion]);
    }
}
