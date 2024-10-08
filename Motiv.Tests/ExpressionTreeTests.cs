using Motiv.ExpressionTrees;

namespace Motiv.Tests;

public class ExpressionTreeTests
{
    [Theory]
    [InlineData(1.5, false)]
    [InlineData(1.0, true)]
    public void Should_evaluate_boolean_expression_from_an_expression_tree_spec(double model, bool expectedResult)
    {
        // Arrange
        var sut =
            Spec.From((double n) => Math.Abs(n % 1) < double.Epsilon)
                .Create("is integer");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Satisfied.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(1.5, false)]
    [InlineData(-1.0, false)]
    [InlineData(-1.5, false)]
    [InlineData(1.0, true)]
    public void Should_evaluate_boolean_expression_containing_an_and(double model, bool expectedResult)
    {
        // Arrange
        var sut =
            Spec.From((double n) => Math.Abs(n % 1) < double.Epsilon & n >= 0)
                .Create("and expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Satisfied.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(1.5, false)]
    [InlineData(-1.0, false)]
    [InlineData(-1.5, false)]
    [InlineData(1.0, true)]
    public void Should_evaluate_boolean_expression_containing_a_conditional_and(double model, bool expectedResult)
    {
        // Arrange
        var sut =
            Spec.From((double n) => Math.Abs(n % 1) < double.Epsilon && n >= 0)
                .Create("conditional and expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Satisfied.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(1.5, true)]
    [InlineData(-1.0, true)]
    [InlineData(-1.5, false)]
    [InlineData(1.0, true)]
    public void Should_evaluate_boolean_expression_containing_an_or(double model, bool expectedResult)
    {
        // Arrange
        var sut =
            Spec.From((double n) => Math.Abs(n % 1) < double.Epsilon | n >= 0)
                .Create("or expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Satisfied.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(1.5, true)]
    [InlineData(-1.0, true)]
    [InlineData(-1.5, false)]
    [InlineData(1.0, true)]
    public void Should_evaluate_boolean_expression_containing_a_conditional_or(double model, bool expectedResult)
    {
        // Arrange
        var sut =
            Spec.From((double n) => Math.Abs(n % 1) < double.Epsilon || n >= 0)
                .Create(" or expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Satisfied.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(1.5, true)]
    [InlineData(-1.0, true)]
    [InlineData(-1.5, false)]
    [InlineData(1.0, false)]
    public void Should_evaluate_boolean_expression_containing_exclusive_or(double model, bool expectedResult)
    {
        // Arrange
        var sut =
            Spec.From((double n) => Math.Abs(n % 1) < double.Epsilon ^ n >= 0)
                .Create("xor expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Satisfied.Should().Be(expectedResult);
    }









    [Theory]
    [InlineData(3, "checked(n + 1) > n")]
    public void Should_assert_expressions_containing_checked_operations(int model, string expectedAssertion)
    {
        // Arrange
        var sut = Spec
            .From((int n) => checked(n + 1) > n)
            .Create("checked-operation");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Fact]
    public void Should_assert_expressions_containing_checked_operations_and_throw_when_overflowing()
    {
        // Arrange
        var sut = Spec
            .From((int n) => checked(n + 1) > n)
            .Create("checked-operation");

        // Act
        var act = () => sut.IsSatisfiedBy(int.MaxValue);

        act.Should().Throw<OverflowException>();
    }

    [Theory]
    [InlineData(5, "Enumerable.Range(1, n).Sum() == 15")]
    [InlineData(3, "Enumerable.Range(1, n).Sum() == 6")]
    public void Should_evaluate_display_as_value_call_on_complex_expressions(int model, string expectedAssertion)
    {
        // Arrange
        var sut = Spec
            .From((int n) => Enumerable.Range(1, n).Sum() == Display.AsValue(n * (n + 1) / 2))
            .Create("linq-method-call");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3 }, "arr.Length == 3")]
    [InlineData(new[] { 1 }, "arr.Length != 3")]
    public void Should_assert_expressions_containing_array_length_property(int[] model, string expectedAssertion)
    {
        // Arrange
        var sut = Spec
            .From((int[] arr) => arr.Length == 3)
            .Create("array-length");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData("test", true, "str.ToCharArray().Any((char c) => char.IsUpper(c)) == false")]
    [InlineData("Test", false, "str.ToCharArray().Any((char c) => char.IsUpper(c)) == true")]
    public void Should_assert_expressions_containing_nested_lambda_expressions(string model, bool expectedResult, string expectedAssertion)
    {
        // Arrange
        var sut = Spec
            .From((string str) => !str.ToCharArray().Any(c => char.IsUpper(c)))
            .Create("nested-lambda");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Satisfied.Should().Be(expectedResult);
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }
}
