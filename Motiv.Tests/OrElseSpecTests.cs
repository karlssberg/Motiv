using System.Text.RegularExpressions;

namespace Motiv.Tests;

public class OrElseSpecTests
{
    [Theory]
    [InlineAutoData(false, false, false, "not left || not right")]
    [InlineAutoData(false, true, true, "right")]
    [InlineAutoData(true, false, true, "left")]
    [InlineAutoData(true, true, true, "left")]
    public void Should_evaluate_as_a_conditional_or(
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

        var spec = left.OrElse((SpecBase<object, string>)right);

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Satisfied;

        // Assert
        act.Should().Be(expectedSatisfied);
    }

    [Theory]
    [InlineAutoData(false, false, "not left || not right")]
    [InlineAutoData(false, true, "right")]
    [InlineAutoData(true, false, "left")]
    [InlineAutoData(true, true, "left")]
    public void Should_provide_a_reason_for_the_conditional_or(
        bool leftValue,
        bool rightValue,
        string expected,
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

        var spec = left.OrElse((SpecBase<object, string>)right);

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().BeEquivalentTo(expected);
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

        var spec = left.OrElse((SpecBase<object, bool>)right);
        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.ToString();

        // Assert
        act.Should().Be(result.Reason);
    }

    [Fact]
    public void Should_not_evaluate_the_right_operand_when_true()
    {
        // Arrange
        var left =
            Spec.Build((object _) => true)
                .WhenTrue("left")
                .WhenFalse("not left")
                .Create();

        var right =
            Spec.Build(new Func<object, bool>(_ => throw new Exception("Should not be evaluated")))
                .WhenTrue("right")
                .WhenFalse("not right")
                .Create();

        var spec = left.OrElse(right);

        // Act
        Action act = () => spec.IsSatisfiedBy(new object());

        // Assert
        act.Should().NotThrow<Exception>();
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

        var spec = left.OrElse((SpecBase<bool, string>)right);

        // Act
        var act = spec.Statement;

        // Assert
        act.Should().Be("left || right");
    }

    [Fact]
    public void Should_describe_in_detail_the_or_else_spec()
    {
        // Arrange
        const string expected =
            """
            OR ELSE
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

        var spec = left.OrElse((SpecBase<bool, string>)right);

        // Act
        var act = spec.Expression;

        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(true, "left")]
    [InlineAutoData(false, "right")]
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

        var spec = left.OrElse((SpecBase<bool, string>)right);

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Reason;

        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(true, """
                        OR
                            left
                        """)]
    [InlineAutoData(false, """
                        OR
                            right
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

        var spec = left.OrElse((SpecBase<bool, string>)right);

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Justification;

        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(true, """
                                OR
                                    left assertion statement
                                """)]
    [InlineAutoData(false,
                                """
                                OR
                                    right assertion statement
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

        var spec = left.OrElse((SpecBase<bool, string>)right);

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Justification;

        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(true, true, true)]
    public void Should_perform_OrElse_on_specs_with_different_metadata(
        bool leftValue,
        bool rightValue,
        bool expectedSatisfied,
        Guid leftTrue,
        Guid leftFalse,
        int  rightTrue,
        int  rightFalse,
        string model)
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

        var spec = left.OrElse(right);

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.Satisfied;

        // Assert
        act.Should().Be(expectedSatisfied);
    }

    [Theory]
    [InlineData(false, false, "¬left", "¬right")]
    [InlineData(false, true, "right")]
    [InlineData(true, false, "left")]
    [InlineData(true, true, "left")]
    public void Should_yield_metadata_for_OrElse_operation_between_different_metadata_types(
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

        var spec = left.OrElse(right);

        var result = spec.IsSatisfiedBy("");

        // Act
        var act = result.Values;

        // Assert
        act.Should().BeEquivalentTo(expectedAssertions);
    }

    [Theory]
    [InlineData(false, false, "¬left", "¬right")]
    [InlineData(false, true, "right")]
    [InlineData(true, false, "left")]
    [InlineData(true, true, "left")]
    public void Should_yield_assertions_for_OrElse_operation_between_different_metadata_types(
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

        var spec = left.OrElse(right);

        var result = spec.IsSatisfiedBy("");

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().BeEquivalentTo(expectedAssertions);
    }

    [Fact]
    public void Should_collapse_OR_ELSE_operators_in_spec_description()
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

        var spec = first.OrElse(second).OrElse(third);

        // Act
        var act = spec.Expression;

        // Assert
        act.Should().Be(
            """
            OR ELSE
                first
                second
                third
            """);
    }

    [Fact]
    public void Should_populate_underlying_results_with_metadata()
    {
        // Arrange
        var left = Spec.Build<object>(_ => false).Create("left");
        var right = Spec.Build<object>(_ => true).Create("right");

        IEnumerable<BooleanResultBase<string>> expected =
        [
            left.IsSatisfiedBy(new object()),
            right.IsSatisfiedBy(new object())
        ];

        var spec = left.OrElse((SpecBase<object, string>)right);
        var result = spec.IsSatisfiedBy(new object());

        // Act
        var act = result.UnderlyingWithMetadata;

        // Assert
        act.Should().BeEquivalentTo(expected);
    }



    [Theory]
    [InlineData(true, true,
        """
        NOR
            left
        """)]
    [InlineData(true, false,
        """
        NOR
            left
        """)]
    [InlineData(false, true,
        """
        NOR
            right
        """)]
    [InlineData(false, false,
        """
        NOR
            ¬left
            ¬right
        """)]
    public void Should_justify_a_nor_creation(bool leftBool, bool rightBool, string expected)
    {
        var left = Spec.Build((bool _) => leftBool).Create("left");
        var right = Spec.Build((bool _) => rightBool).Create("right");

        var spec = !(left.OrElse((SpecBase<bool, string>)right));

        var result = spec.IsSatisfiedBy(false);

        result.Justification.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, true,
        """
        OR
            left
        """)]
    [InlineData(true, false,
        """
        OR
            left
        """)]
    [InlineData(false, true,
        """
        OR
            right
        """)]
    [InlineData(false, false,
        """
        OR
            ¬left
            ¬right
        """)]
    public void Should_justify_a_nor_negation(bool leftBool, bool rightBool, string expected)
    {
        var left = Spec.Build((bool _) => leftBool).Create("left");
        var right = Spec.Build((bool _) => rightBool).Create("right");

        var spec = !!(left.OrElse((SpecBase<bool, string>)right));

        var result = spec.IsSatisfiedBy(false);

        result.Justification.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, true,
        """
        NOR
            left
        """)]
    [InlineData(true, false,
        """
        NOR
            left
        """)]
    [InlineData(false, true,
        """
        NOR
            right
        """)]
    [InlineData(false, false,
        """
        NOR
            ¬left
            ¬right
        """)]
    public void Should_justify_a_nor_double_negation(bool leftBool, bool rightBool, string expected)
    {
        var left = Spec.Build((bool _) => leftBool).Create("left");
        var right = Spec.Build((bool _) => rightBool).Create("right");

        var spec = !!!(left.OrElse((SpecBase<bool, string>)right));

        var result = spec.IsSatisfiedBy(false);

        result.Justification.Should().Be(expected);
    }
}
