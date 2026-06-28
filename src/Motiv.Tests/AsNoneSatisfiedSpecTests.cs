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
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var spec = Spec
            .Build(underlyingSpec)
            .AsNoneSatisfied()
            .Create("none are true");

        var result = spec.Evaluate([first, second, third]);

        // Act
        var act = result.Satisfied;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                        none are true == true
                                            false (3)
                                        """)]
    [InlineAutoData(false, false, true, """
                                        none are true == false
                                            true (1)
                                        """)]
    [InlineAutoData(false, true, false, """
                                        none are true == false
                                            true (1)
                                        """)]
    [InlineAutoData(false, true, true, """
                                        none are true == false
                                            true (2)
                                        """)]
    [InlineAutoData(true, false, false, """
                                        none are true == false
                                            true (1)
                                        """)]
    [InlineAutoData(true, false, true, """
                                        none are true == false
                                            true (2)
                                        """)]
    [InlineAutoData(true, true, false, """
                                        none are true == false
                                            true (2)
                                        """)]
    [InlineAutoData(true, true, true, """
                                        none are true == false
                                            true (3)
                                        """)]
    public void Should_serialize_the_result_of_the_all_operation_when_metadata_is_a_string(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        // Arrange
        var underlyingSpec =
            Spec.Build((bool m) => m)
                .WhenTrue(true.ToString().ToLowerInvariant())
                .WhenFalse(false.ToString().ToLowerInvariant())
                .Create();

        var spec =
            Spec.Build(underlyingSpec)
                .AsNoneSatisfied()
                .WhenTrueYield(evaluation => evaluation.Metadata)
                .WhenFalseYield(evaluation => evaluation.Metadata)
                .Create("none are true");

        var result = spec.Evaluate([first, second, third]);

        // Act
        var act = result.Justification;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                        none are true == true
                                            false (3)
                                        """)]
    [InlineAutoData(false, false, true, """
                                        none are true == false
                                            true (1)
                                        """)]
    [InlineAutoData(false, true, false, """
                                        none are true == false
                                            true (1)
                                        """)]
    [InlineAutoData(false, true, true, """
                                        none are true == false
                                            true (2)
                                        """)]
    [InlineAutoData(true, false, false, """
                                        none are true == false
                                            true (1)
                                        """)]
    [InlineAutoData(true, false, true, """
                                        none are true == false
                                            true (2)
                                        """)]
    [InlineAutoData(true, true, false, """
                                        none are true == false
                                            true (2)
                                        """)]
    [InlineAutoData(true, true, true, """
                                        none are true == false
                                            true (3)
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
            .AsNoneSatisfied()
            .WhenTrueYield(evaluation => evaluation.Metadata)
            .WhenFalseYield(evaluation => evaluation.Metadata)
            .Create("none are true");


        var result = spec.Evaluate([first, second, third]);

        // Act
        var act = result.Justification;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, """
                                        none are true == true
                                            is true == false (3)
                                        """)]
    [InlineAutoData(false, false, true, """
                                        none are true == false
                                            is true == true (1)
                                        """)]
    [InlineAutoData(false, true, false, """
                                        none are true == false
                                            is true == true (1)
                                        """)]
    [InlineAutoData(false, true, true, """
                                        none are true == false
                                            is true == true (2)
                                        """)]
    [InlineAutoData(true, false, false, """
                                        none are true == false
                                            is true == true (1)
                                        """)]
    [InlineAutoData(true, false, true, """
                                        none are true == false
                                            is true == true (2)
                                        """)]
    [InlineAutoData(true, true, false, """
                                        none are true == false
                                            is true == true (2)
                                        """)]
    [InlineAutoData(true, true, true, """
                                        none are true == false
                                            is true == true (3)
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
            .AsNoneSatisfied()
            .WhenTrueYield(evaluation => evaluation.Metadata)
            .WhenFalseYield(evaluation => evaluation.Metadata)
            .Create("none are true");

        var result = spec.Evaluate([first, second, third]);

        // Act
        var act = result.Justification;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, false, true)]
    [InlineAutoData(false, true, false)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(true, false, false)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(true, true, false)]
    [InlineAutoData(true, true, true)]
    public void Should_explain_in_full_the_result_of_the_all_operation_and_show_multiple_underlying_causes(
        bool first,
        bool second,
        bool third)
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

        var spec = Spec
            .Build(underlyingSpecLeft & underlyingSpecRight)
            .AsNoneSatisfied()
            .WhenTrueYield(evaluation => evaluation.Values)
            .WhenFalseYield(evaluation => evaluation.Values)
            .Create("none are true");

        var result = spec.Evaluate([first, second, third]);
        var satisfied = result.Satisfied;
        var suffix = satisfied ? "false" : "true";
        var causalCount = new[] { first, second, third }.Count(b => b != satisfied);

        var expected = string.Join(Environment.NewLine,
            $"none are true == {satisfied.ToString().ToLowerInvariant()}",
            $"    AND ({causalCount})",
            $"        left == {suffix}",
            $"        right == {suffix}");

        // Act
        var act = result.Justification;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "none are true == true")]
    [InlineAutoData(false, false, true, "none are true == false")]
    [InlineAutoData(false, true, false, "none are true == false")]
    [InlineAutoData(false, true, true, "none are true == false")]
    [InlineAutoData(true, false, false, "none are true == false")]
    [InlineAutoData(true, false, true, "none are true == false")]
    [InlineAutoData(true, true, false, "none are true == false")]
    [InlineAutoData(true, true, true, "none are true == false")]
    public void Should_Describe_the_result_of_the_all_operation_and_show_multiple_underlying_causes(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        // Arrange
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

        var spec =
            Spec.Build(underlyingSpecLeft & underlyingSpecRight)
                .AsNoneSatisfied()
                .WhenTrueYield(evaluation => evaluation.Values)
                .WhenFalseYield(evaluation => evaluation.Values)
                .Create("none are true");

        var result = spec.Evaluate([first, second, third]);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false, "none are true == true")]
    [InlineAutoData(false, false, true, "none are true == false")]
    [InlineAutoData(false, true, false, "none are true == false")]
    [InlineAutoData(false, true, true, "none are true == false")]
    [InlineAutoData(true, false, false, "none are true == false")]
    [InlineAutoData(true, false, true, "none are true == false")]
    [InlineAutoData(true, true, false, "none are true == false")]
    [InlineAutoData(true, true, true, "none are true == false")]
    public void Should_serialize_the_result_of_the_all_operation_and_show_multiple_underlying_causes(
        bool first,
        bool second,
        bool third,
        string expected)
    {
        // Arrange
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

        var spec =
            Spec.Build(underlyingSpecLeft & underlyingSpecRight)
                .AsNoneSatisfied()
                .WhenTrueYield(evaluation => evaluation.Values)
                .WhenFalseYield(evaluation => evaluation.Values)
                .Create("none are true");

        var result = spec.Evaluate([first, second, third]);

        // Act
        var act = result.ToString();

        // Assert
        act.ShouldBe(expected);
    }

    [Fact]
    public void Should_provide_a_statement_about_the_specification()
    {
        // Arrange
        const string expectedSummary = "all booleans are true";

        var underlyingSpec =
            Spec.Build((bool m) => m)
                .WhenTrue(true.ToString())
                .WhenFalse(false.ToString())
                .Create("is true");

        var spec =
            Spec.Build(underlyingSpec)
                .AsNoneSatisfied()
                .WhenTrue("all  true")
                .WhenFalse(evaluation => $"{evaluation.FalseCount} false")
                .Create("all booleans are true");

        // Act
        var act = spec.Name;

        // Assert
        act.ShouldBe(expectedSummary);
    }

    [Fact]
    public void Should_provide_a_serialized_explanation_of_the_specification()
    {
        // Arrange
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

        var spec =
            Spec.Build(underlyingSpec)
                .AsNoneSatisfied()
                .WhenTrue("all  true")
                .WhenFalse(evaluation => $"{evaluation.FalseCount} false")
                .Create("all booleans are true");

        // Act
        var act = spec.Expression;

        // Assert
        act.ShouldBe(expectedFull);
    }

    [Fact]
    public void Should_use_the_original_propositional_statement_of_the_specification()
    {
        // Arrange
        const string expectedSummary = "all booleans are true";

        var underlyingSpec =
            Spec.Build((bool m) => m)
                .WhenTrue(true.ToString())
                .WhenFalse(false.ToString())
                .Create("is true");

        var spec =
            Spec.Build(underlyingSpec)
                .AsNoneSatisfied()
                .WhenTrue("all  true")
                .WhenFalse(evaluation => $"{evaluation.FalseCount} false")
                .Create("all booleans are true");

        // Act
        var act = spec.Name;

        // Assert
        act.ShouldBe(expectedSummary);
    }

    [Fact]
    public void Should_have_a_serilialized_representation_of_the_logic_of_the_specification()
    {
        // Arrange
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

        var spec =
            Spec.Build(underlyingSpec)
                .AsNoneSatisfied()
                .WhenTrue("all  true")
                .WhenFalse(evaluation => $"{evaluation.FalseCount} false")
                .Create("all booleans are true");

        // Act
        var act = spec.Expression;

        // Assert
        act.ShouldBe(expectedFull);
    }

    [Fact]
    public void Should_output_the_proposition_statement_when_calling_ToString_of_the_specification()
    {
        // Arrange
        const string expectedSummary = "all booleans are true";

        var underlyingSpec =
            Spec.Build((bool m) => m)
                .WhenTrue(true.ToString())
                .WhenFalse(false.ToString())
                .Create("is true");

        var spec =
            Spec.Build(underlyingSpec)
                .AsNoneSatisfied()
                .WhenTrue("all  true")
                .WhenFalse(evaluation => $"{evaluation.FalseCount} false")
                .Create("all booleans are true");

        // Act
        var act = spec.ToString();

        // Assert
        act.ShouldBe(expectedSummary);
    }

    [Fact]
    public void Should_provide_a_description_of_the_specification_when_metadata_is_a_string()
    {
        // Arrange
        const string expectedSummary = "none are true";

        var underlyingSpec =
            Spec.Build((bool m) => m)
                .WhenTrue("is true")
                .WhenFalse("is false")
                .Create();

        var spec =
            Spec.Build(underlyingSpec)
                .AsNoneSatisfied()
                .WhenTrue(true)
                .WhenFalse(false)
                .Create("none are true");

        // Act
        var act = spec.ToString();

        // Assert
        act.ShouldBe(expectedSummary);
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, false)]
    public void Should_perform_a_none_satisfied_operation_when_using_a_boolean_predicate_function(
        bool first,
        bool second,
        bool third,
        bool expected)
    {
        // Arrange
        var spec =
            Spec.Build((bool m) => m)
                .AsNoneSatisfied()
                .WhenTrue(_ => "none are true")
                .WhenFalse(_ => "none are true")
                .Create("none true");

        var result = spec.Evaluate([first, second, third]);

        // Act
        var act = result.Satisfied;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData(false, false, false, "none true == true")]
    [InlineData(false, false, true, "none true == false")]
    [InlineData(false, true, false, "none true == false")]
    [InlineData(false, true, true, "none true == false")]
    [InlineData(true, false, false, "none true == false")]
    [InlineData(true, false, true, "none true == false")]
    [InlineData(true, true, false, "none true == false")]
    [InlineData(true, true, true, "none true == false")]
    public void Should_provide_a_reason_for_a_none_satisfied_operation_when_using_a_boolean_predicate_function(
        bool first,
        bool second,
        bool third,
        string expectedReason)
    {
        // Arrange
        var spec =
            Spec.Build((bool m) => m)
                .AsNoneSatisfied()
                .WhenTrue(_ => "none are true")
                .WhenFalse(_ => "some are true")
                .Create("none true");

        var result = spec.Evaluate([first, second, third]);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expectedReason);
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, false)]
    public void Should_perform_a_none_satisfied_operation_when_using_a_boolean_result_predicate_function(
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
            Spec.Build((bool model) => underlying.Evaluate(model))
                .AsNoneSatisfied()
                .WhenTrue(_ => "none are true")
                .WhenFalse(_ => "none are true")
                .Create("none true");

        var result = spec.Evaluate([first, second, third]);

        // Act
        var act = result.Satisfied;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData(false, false, false, "none true == true")]
    [InlineData(false, false, true, "none true == false")]
    [InlineData(false, true, false, "none true == false")]
    [InlineData(false, true, true, "none true == false")]
    [InlineData(true, false, false, "none true == false")]
    [InlineData(true, false, true, "none true == false")]
    [InlineData(true, true, false, "none true == false")]
    [InlineData(true, true, true, "none true == false")]
    public void Should_provide_a_reason_for_a_none_satisfied_operation_when_using_a_boolean_result_predicate_function(
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
            Spec.Build((bool model) => underlying.Evaluate(model))
                .AsNoneSatisfied()
                .WhenTrue(_ => "none are true")
                .WhenFalse(_ => "none are true")
                .Create("none true");

        var result = spec.Evaluate([first, second, third]);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expectedReason);
    }
}

