using System.Text.RegularExpressions;

namespace Motiv.Tests;

public class OrSpecTests
{
    [Theory]
    [InlineAutoData(true, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(false, false, false)]
    public void Should_perform_logical_or_operation(
        bool leftResult,
        bool rightResult,
        bool expected,
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

        var spec = left | right;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Satisfied;

        // Assert
        act.ShouldBe(expected);
    }


    [Theory]
    [InlineAutoData(true, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(false, false, false)]
    public void Should_yield_metadata_for_logical_or_operation(
        bool leftResult,
        bool rightResult,
        bool expected,
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

        var spec = left | right;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Values;

        // Assert
        act.ShouldBe([expected]);
    }

    [Theory]
    [InlineAutoData(true, true, "(left == true) | (right == true)")]
    [InlineAutoData(true, false, "left == true")]
    [InlineAutoData(false, true, "right == true")]
    [InlineAutoData(false, false, "(left == false) | (right == false)")]
    public void Should_serialize_the_result_of_the_or_operation(
        bool leftResult,
        bool rightResult,
        string expected,
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

        var spec = left | right;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expected);
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

        var spec = left | right;
        var result = spec.Evaluate(model);

        // Act
        var act = result.ToString();

        // Assert
        act.ShouldBe(result.Reason);
    }

    [Theory]
    [InlineAutoData(true, true, "(left == true) | (right == true)")]
    [InlineAutoData(true, false, "left == true")]
    [InlineAutoData(false, true, "right == true")]
    [InlineAutoData(false, false, "(left == false) | (right == false)")]
    public void Should_serialize_the_result_of_the_or_operation_when_metadata_is_a_string(
        bool leftResult,
        bool rightResult,
        string expected,
        object model)
    {
        // Arrange
        var left = Spec
            .Build<object>(_ => leftResult)
            .Create("left");


        var right = Spec
            .Build<object>(_ => rightResult)
            .Create("right");

        var spec = left | right;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(true, true, "True | True")]
    [InlineAutoData(true, false, "True")]
    [InlineAutoData(false, true, "True")]
    [InlineAutoData(false, false, "False | False")]
    public void Should_serialize_the_result_of_the_or_operation_when_metadata_is_a_string_when_using_the_single_generic_specification_type(
        bool leftResult,
        bool rightResult,
        string expected,
        object model)
    {
        // Arrange
        var left = Spec
            .Build<object>(_ => leftResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var spec = left | right;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Reason;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(true, true)]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(false, false)]
    public void Should_provide_a_statement_of_the_metadata_specification(bool leftResult, bool rightResult)
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

        var expected = $"{left.Name} | {right.Name}";

        var spec = left | right;

        // Act
        var act = spec.Name;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(true, true)]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(false, false)]
    public void Should_serialize_a_description_of_the_metadata_specification(bool leftResult, bool rightResult)
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

        var expected = $"{left.Name} | {right.Name}";

        var spec = left | right;

        // Act
        var act = spec.ToString();

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(true, true)]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(false, false)]
    public void Should_provide_a_statement_about_the_explanation_specification(bool leftResult, bool rightResult)
    {
        // Arrange
        var left = Spec
            .Build<object>(_ => leftResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var expected = $"{left.Name} | {right.Name}";

        var spec = left | right;

        // Act
        var act = spec.Name;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(true, true)]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(false, false)]
    public void Should_serialize_the_implicit_statement_about_the_explanation_specification(bool leftResult, bool rightResult)
    {
        // Arrange
        var left = Spec
            .Build<object>(_ => leftResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue(true.ToString())
            .WhenFalse(false.ToString())
            .Create();

        var expected = $"{left.Name} | {right.Name}";

        var spec = left | right;

        // Act
        var act = spec.ToString();

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(false, false, 2)]
    [InlineAutoData(false, true, 1)]
    [InlineAutoData(true, false, 1)]
    [InlineAutoData(true, true, 2)]
    public void Should_accurately_report_the_number_of_causal_operands(bool left, bool right, int expected,
        object model)
    {
        // Arrange
        var leftSpec = Spec
            .Build<object>(_ => left)
            .Create("left");

        var rightSpec = Spec
            .Build<object>(_ => right)
            .Create("right");

        var spec = leftSpec | rightSpec;

        var result = spec.Evaluate(model);

        // Act
        var act = result.Description.CausalOperandCount;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(true, true, true)]
    public void Should_perform_Or_on_specs_with_different_metadata(
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

        var spec = left | right;

        var result = spec.Evaluate("");

        // Act
        var act = result.Satisfied;

        // Assert
        act.ShouldBe(expectedSatisfied);
    }

    [Theory]
    [InlineData(false, false, "left == false", "right == false")]
    [InlineData(false, true, "right == true")]
    [InlineData(true, false, "left == true")]
    [InlineData(true, true, "left == true", "right == true")]
    public void Should_yield_assertions_for_different_metadata_types(
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

        var spec = left | right;

        var result = spec.Evaluate("");

        // Act
        var act = result.Assertions;

        // Assert
        act.ShouldBe(expectedAssertions);
    }

    [Theory]
    [InlineData(false, false, "left == false", "right == false")]
    [InlineData(false, true, "right == true")]
    [InlineData(true, false, "left == true")]
    [InlineData(true, true, "left == true", "right == true")]
    public void Should_yield_metadata_for_different_metadata_types(
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

        var spec = left | right;

        var result = spec.Evaluate("");

        // Act
        var act = result.Values;

        // Assert
        act.ShouldBe(expectedAssertions);
    }

    [Fact]
    public void Should_not_collapse_OR_operators_in_spec_description()
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

        var spec = first | second | third;

        // Act
        var act = spec.Expression;

        // Assert
        act.ShouldBe(
            """
            OR
                first
                second
                third
            """);
    }

    [Fact]
    public void Should_populate_underlying_results()
    {
        // Arrange
        var left = Spec.Build<object>(_ => true).Create("left");
        var right = Spec.Build<object>(_ => true).Create("right");

        IEnumerable<BooleanResultBase<string>> expected =
        [
            left.Evaluate(new object()),
            right.Evaluate(new object())
        ];

        var spec = left | right;
        var result = spec.Evaluate(new object());

        // Act
        var act = result.Underlying;

        // Assert
        act.ShouldBe(expected);
    }

    [Fact]
    public void Should_populate_underlying_results_with_metadata()
    {
        // Arrange
        var left = Spec.Build<object>(_ => true).Create("left");
        var right = Spec.Build<object>(_ => true).Create("right");

        IEnumerable<BooleanResultBase<string>> expected =
        [
            left.Evaluate(new object()),
            right.Evaluate(new object())
        ];

        var spec = left | right;
        var result = spec.Evaluate(new object());

        // Act
        var act = result.UnderlyingWithValues;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, true,
        """
        NOR
            left == true
            right == true
        """)]
    [InlineData(true, false,
        """
        NOR
            left == true
        """)]
    [InlineData(false, true,
        """
        NOR
            right == true
        """)]
    [InlineData(false, false,
        """
        NOR
            left == false
            right == false
        """)]
    public void Should_justify_a_nor_creation(bool leftBool, bool rightBool, string expected)
    {
        var left = Spec.Build((bool _) => leftBool).Create("left");
        var right = Spec.Build((bool _) => rightBool).Create("right");

        var spec = !(left | right);

        var result = spec.Evaluate(false);

        result.Justification.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, true,
        """
        OR
            left == true
            right == true
        """)]
    [InlineData(true, false,
        """
        OR
            left == true
        """)]
    [InlineData(false, true,
        """
        OR
            right == true
        """)]
    [InlineData(false, false,
        """
        OR
            left == false
            right == false
        """)]
    public void Should_justify_a_nor_negation(bool leftBool, bool rightBool, string expected)
    {
        var left = Spec.Build((bool _) => leftBool).Create("left");
        var right = Spec.Build((bool _) => rightBool).Create("right");

        var spec = !!(left | right);

        var result = spec.Evaluate(false);

        result.Justification.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, true,
        """
        NOR
            left == true
            right == true
        """)]
    [InlineData(true, false,
        """
        NOR
            left == true
        """)]
    [InlineData(false, true,
        """
        NOR
            right == true
        """)]
    [InlineData(false, false,
        """
        NOR
            left == false
            right == false
        """)]
    public void Should_justify_a_nor_double_negation(bool leftBool, bool rightBool, string expected)
    {
        var left = Spec.Build((bool _) => leftBool).Create("left");
        var right = Spec.Build((bool _) => rightBool).Create("right");

        var spec = !!!(left | right);

        var result = spec.Evaluate(false);

        result.Justification.ShouldBe(expected);
    }
}

