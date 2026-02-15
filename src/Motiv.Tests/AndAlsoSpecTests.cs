using System.Text.RegularExpressions;

namespace Motiv.Tests;

public class AndAlsoSpecTests
{
    [Theory]
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, false)]
    [InlineAutoData(true, false, false)]
    [InlineAutoData(true, true, true)]
    public void Should_evaluate_as_a_logical_and_with_short_circuiting(
        bool leftValue,
        bool rightValue,
        bool expectedSatisfied,
        object model)
    {
        // Arrange
        var left =
            Spec.Build((object _) => leftValue)
                .WhenTrue("left")
                .WhenFalse("not left")
                .Create();

        var right =
            Spec.Build((object _) => rightValue)
                .WhenTrue("right")
                .WhenFalse("not right")
                .Create();

        var spec = left.AndAlso(right);
        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Satisfied;

        // Assert
        act.ShouldBe(expectedSatisfied);
    }

    [Theory]
    [InlineAutoData(false, false, "not left")]
    [InlineAutoData(false, true, "not left")]
    [InlineAutoData(true, false, "not right")]
    [InlineAutoData(true, true, "(left) && (right)")]
    public void Should_evaluate_reasons(
        bool leftValue,
        bool rightValue,
        string expectedSerialized,
        object model)
    {
        // Arrange
        var left =
            Spec.Build((object _) => leftValue)
                .WhenTrue("left")
                .WhenFalse("not left")
                .Create();

        var right =
            Spec.Build((object _) => rightValue)
                .WhenTrue("right")
                .WhenFalse("not right")
                .Create();

        var spec = left.AndAlso(right);
        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expectedSerialized);
    }

    [Theory]
    [InlineAutoData(true, true)]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(false, false)]
    public void Should_use_the_reason_property_for_ToString(
        bool leftResult,
        bool rightResult,
        object model)
    {
        // Arrange
        var left = Spec
            .Build<object>(_ => leftResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("left");

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("right");

        var spec = left.AndAlso(right);
        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.ToString();

        // Assert
        act.ShouldBe(result.Reason);
    }

    [Fact]
    public void Should_not_evaluate_the_right_operand_when_false()
    {
        // Arrange
        var left =
            Spec.Build((object _) => false)
                .WhenTrue("left")
                .WhenFalse("not left")
                .Create();

        var right =
            Spec.Build(new Func<object, bool>(_ => throw new Exception("Should not be evaluated")))
                .WhenTrue("right")
                .WhenFalse("not right")
                .Create();

        var spec = left.AndAlso(right);

        // Act
        Action act = () => spec.IsSatisfiedBy(new object());

        // Assert
        act.ShouldNotThrow();
    }

    [Fact]
    public void Should_have_spec_with_propositional_statement()
    {
        // Arrange
        var left =
            Spec.Build((bool m) => m)
                .WhenTrue("left")
                .WhenFalse("not left")
                .Create();

        var right =
            Spec.Build((bool m) => !m)
                .WhenTrue("right")
                .WhenFalse("not right")
                .Create();

        var spec = left.AndAlso(right);

        // Act
        var act = spec.Name;

        // Assert
        act.ShouldBe("left && right");
    }

    [Fact]
    public void Should_describe_in_detail_the_or_else_spec()
    {
        // Arrange
        const string expected =
            """
            AND ALSO
                left
                right
            """;

        var left =
            Spec.Build((bool m) => m)
                .WhenTrue("left")
                .WhenFalse("not left")
                .Create();

        var right =
            Spec.Build((bool m) => !m)
                .WhenTrue("right")
                .WhenFalse("not right")
                .Create();

        var spec = left.AndAlso(right);

        // Act
        var act = spec.Expression;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(true, "not right")]
    [InlineAutoData(false, "not left")]
    public void Should_describe_the_result(bool model, string expected)
    {
        // Arrange
        var left =
            Spec.Build((bool m) => m)
                .WhenTrue("left")
                .WhenFalse("not left")
                .Create();

        var right =
            Spec.Build((bool m) => !m)
                .WhenTrue("right")
                .WhenFalse("not right")
                .Create();

        var spec = left.AndAlso(right);

        // Act
        var act = spec.IsSatisfiedBy(model).Description.Reason;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(true, """
                                    AND
                                        not right
                                    """)]
    [InlineAutoData(false, """
                                    AND
                                        not left
                                    """)]
    public void Should_describe_the_result_in_detail_over_a_single_line_because_operands_are_short(bool model, string expected)
    {
        // Arrange
        var left =
            Spec.Build((bool m) => m)
                .WhenTrue("left")
                .WhenFalse("not left")
                .Create();

        var right =
            Spec.Build((bool m) => !m)
                .WhenTrue("right")
                .WhenFalse("not right")
                .Create();

        var spec = left.AndAlso(right);

        // Act
        var act = spec.IsSatisfiedBy(model).Justification;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(true, """
                            AND
                                not right assertion statement
                            """)]
    [InlineAutoData(false, """
                            AND
                                not left assertion statement
                            """)]
    public void Should_describe_the_result_in_detail_over_multiple_lines_because_operands_are_long(bool model, string expected)
    {
        // Arrange
        var left =
            Spec.Build((bool m) => m)
                .WhenTrue("left assertion statement")
                .WhenFalse("not left assertion statement")
                .Create();

        var right =
            Spec.Build((bool m) => !m)
                .WhenTrue("right assertion statement")
                .WhenFalse("not right assertion statement")
                .Create();

        var spec = left.AndAlso(right);

        // Act
        var act = spec.IsSatisfiedBy(model).Justification;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, false)]
    [InlineAutoData(true, false, false)]
    [InlineAutoData(true, true, true)]
    public void Should_perform_AndAlso_on_specs_with_different_metadata(
        bool leftValue,
        bool rightValue,
        bool expectedSatisfied,
        Guid leftTrue,
        Guid leftFalse,
        int  rightTrue,
        int  rightFalse)
    {
        // Arrange
        var left =
            Spec.Build((string _) => leftValue)
                .WhenTrue(leftTrue)
                .WhenFalse(leftFalse)
                .Create("left");

        var right =
            Spec.Build((string _) => rightValue)
                .WhenTrue(rightTrue)
                .WhenFalse(rightFalse)
                .Create("right");

        var spec = left.AndAlso(right);

        // Act
        var act = spec.IsSatisfiedBy("").Satisfied;

        // Assert
        act.ShouldBe(expectedSatisfied);
    }

    [Theory]
    [InlineData(false, false, "left == false")]
    [InlineData(false, true, "left == false")]
    [InlineData(true, false, "right == false")]
    [InlineData(true, true, "left == true", "right == true")]
    public void Should_perform_AndAlso_on_specs_with_different_metadata_and_preserve_assertions(
        bool leftValue,
        bool rightValue,
        params string[] expectedAssertions)
    {
        // Arrange
        var left =
            Spec.Build((string _) => leftValue)
                .WhenTrue(new Uri("http://true"))
                .WhenFalse(new Uri("http://false"))
                .Create("left");

        var right =
            Spec.Build((string _) => rightValue)
                .WhenTrue(new Regex("true"))
                .WhenFalse(new Regex("false"))
                .Create("right");

        var spec = left.AndAlso(right);

        // Act
        var act = spec.IsSatisfiedBy("").Assertions;

        // Assert
        act.ShouldBe(expectedAssertions);
    }

    [Theory]
    [InlineData(false, false, "left == false")]
    [InlineData(false, true, "left == false")]
    [InlineData(true, false, "right == false")]
    [InlineData(true, true, "left == true", "right == true")]
    public void Should_perform_AndAlso_on_specs_with_different_metadata_and_preserve_metadata(
        bool leftValue,
        bool rightValue,
        params string[] expectedAssertions)
    {
        // Arrange
        var left =
            Spec.Build((string _) => leftValue)
                .WhenTrue(new Uri("http://true"))
                .WhenFalse(new Uri("http://false"))
                .Create("left");

        var right =
            Spec.Build((string _) => rightValue)
                .WhenTrue(new Regex("true"))
                .WhenFalse(new Regex("false"))
                .Create("right");

        var spec = left.AndAlso(right);

        // Act
        var act = spec.IsSatisfiedBy("").Values;

        // Assert
        act.ShouldBe(expectedAssertions);
    }

    [Fact]
    public void Should_not_collapse_ORELSE_operators_in_spec_description()
    {
        // Arrange
        var first = Spec
            .Build<bool>(_ => true)
            .Create("first");

        var second = Spec
            .Build<bool>(_ => true)
            .Create("second");

        var third = Spec
            .Build<bool>(_ => true)
            .Create("third");

        var spec = first.AndAlso(second).AndAlso(third);

        spec.Expression.ShouldBe(
            """
            AND ALSO
                first
                second
                third
            """);
    }

    [Fact]
    public void Should_return_the_underlying_specs()
    {
        // Arrange
        var left = Spec
            .Build<bool>(_ => true)
            .Create("left");

        var right = Spec
            .Build<bool>(_ => true)
            .Create("right");

        var spec = left.AndAlso(right);

        // Act
        var act = spec.Underlying;

        // Assert
        act.ShouldBe((PolicyBase<bool, string>[])[left, right]);
    }

    [Fact]
    public void Should_populate_underlying_results_with_metadata()
    {
        // Arrange
        var left = Spec.Build<object>(_ => true).Create("left");
        var right = Spec.Build<object>(_ => true).Create("right");

        IEnumerable<BooleanResultBase<string>> expected =
        [
            left.IsSatisfiedBy(new object()),
            right.IsSatisfiedBy(new object())
        ];

        var spec = left.AndAlso(right);
        var result = spec.IsSatisfiedBy(new object());

        // Act
        var act = result.UnderlyingWithValues;

        // Assert
        act.ShouldBe(expected);
    }



    [Theory]
    [InlineData(true, true,
        """
        NAND ALSO
            left == true
            right == true
        """)]
    [InlineData(true, false,
        """
        NAND ALSO
            right == false
        """)]
    [InlineData(false, true,
        """
        NAND ALSO
            left == false
        """)]
    [InlineData(false, false,
        """
        NAND ALSO
            left == false
        """)]
    public void Should_justify_a_nand_creation(bool leftBool, bool rightBool, string expected)
    {
        var left = Spec.Build((bool _) => leftBool).Create("left");
        var right = Spec.Build((bool _) => rightBool).Create("right");

        var spec = !(left.AndAlso(right));

        var result = spec.IsSatisfiedBy(false);

        result.Justification.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, true,
        """
        AND ALSO
            left == true
            right == true
        """)]
    [InlineData(true, false,
        """
        AND ALSO
            right == false
        """)]
    [InlineData(false, true,
        """
        AND ALSO
            left == false
        """)]
    [InlineData(false, false,
        """
        AND ALSO
            left == false
        """)]
    public void Should_justify_a_nand_negation(bool leftBool, bool rightBool, string expected)
    {
        var left = Spec.Build((bool _) => leftBool).Create("left");
        var right = Spec.Build((bool _) => rightBool).Create("right");

        var spec = !!(left.AndAlso(right));

        var result = spec.IsSatisfiedBy(false);

        result.Justification.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, true,
        """
        NAND ALSO
            left == true
            right == true
        """)]
    [InlineData(true, false,
        """
        NAND ALSO
            right == false
        """)]
    [InlineData(false, true,
        """
        NAND ALSO
            left == false
        """)]
    [InlineData(false, false,
        """
        NAND ALSO
            left == false
        """)]
    public void Should_justify_a_nand_double_negation(bool leftBool, bool rightBool, string expected)
    {
        var left = Spec.Build((bool _) => leftBool).Create("left");
        var right = Spec.Build((bool _) => rightBool).Create("right");

        var spec = !!!(left.AndAlso(right));

        var result = spec.IsSatisfiedBy(false);

        result.Justification.ShouldBe(expected);
    }
}

