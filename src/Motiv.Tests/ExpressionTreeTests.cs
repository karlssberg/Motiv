using Shouldly;

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
        act.Satisfied.ShouldBe(expectedResult);
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
        act.Satisfied.ShouldBe(expectedResult);
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
        act.Satisfied.ShouldBe(expectedResult);
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
        act.Satisfied.ShouldBe(expectedResult);
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
        act.Satisfied.ShouldBe(expectedResult);
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
        act.Satisfied.ShouldBe(expectedResult);
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
        act.Assertions.ShouldBe([expectedAssertion]);
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
        act.Assertions.ShouldBe([expectedAssertion]);
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
        act.Assertions.ShouldBe([expectedAssertion]);
    }

    [Theory]
    [InlineData("test", true, "char.IsUpper(c) == false")]
    [InlineData("Test", false, "char.IsUpper(c) == true")]
    public void Should_assert_expressions_containing_nested_lambda_expressions(string model, bool expectedResult, string expectedAssertion)
    {
        // Arrange
        var sut = Spec
            .From((string str) => !str.ToCharArray().Any(c => char.IsUpper(c)))
            .Create("nested-lambda");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Satisfied.ShouldBe(expectedResult);
        act.Assertions.ShouldBe([expectedAssertion]);
    }

    [Theory]
    [InlineData("test", true, "str.ToCharArray().Any(Char.IsUpper) == false")]
    [InlineData("Test", false, "str.ToCharArray().Any(Char.IsUpper) == true")]
    public void Should_assert_expressions_containing_a_method_group(string model, bool expectedResult, string expectedAssertion)
    {
        // Arrange
        var sut = Spec
            .From((string str) => !str.ToCharArray().Any(char.IsUpper))
            .Create("nested-lambda");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Satisfied.ShouldBe(expectedResult);
        act.Assertions.ShouldBe([expectedAssertion]);
    }

    [Theory]
    [InlineData(typeof(string), true, "typeof(IEnumerable<char>).IsAssignableFrom(t) == true")]
    [InlineData(typeof(int), false, "typeof(IEnumerable<char>).IsAssignableFrom(t) == false")]
    public void Should_evaluate_type_testing_expressions(Type type, bool expectedResult, params string[] expectedAssertion)
    {
        // Arrange
        var sut = Spec
            .From((Type t) => typeof(IEnumerable<char>).IsAssignableFrom(t))
            .Create("type-test");

        // Act
        var act = sut.IsSatisfiedBy(type);

        // Assert
        act.Satisfied.ShouldBe(expectedResult);
        act.Assertions.ShouldBe(expectedAssertion);
    }

    [Theory]
    [InlineData(1, true, "n * 2 / 2 + 1 - 1 == 1", "n < 1073741823")]
    [InlineData(2147483647, false, "n * 2 / 2 + 1 - 1 != 2147483647")] // int.MaxValue
    public void Should_evaluate_compound_arithmetic_expressions(int value, bool expectedResult, params string[] expectedAssertion)
    {
        // Arrange
        var sut = Spec
            .From((int n) => n * 2 / 2 + 1 - 1 == Display.AsValue(n) && n < int.MaxValue / 2)
            .Create("compound-arithmetic");

        // Act
        var act = sut.IsSatisfiedBy(value);

        // Assert
        act.Satisfied.ShouldBe(expectedResult);
        act.Assertions.ShouldBe(expectedAssertion);
    }

    [Theory]
    [InlineData(new[] { 1.5, 2.5 }, true, "arr[0] % 1 > 0", "arr[1] % 1 > 0")]
    [InlineData(new[] { 1.0, 2.0 }, false, "arr[0] % 1 <= 0")]
    public void Should_evaluate_array_element_access_with_complex_conditions(double[] values, bool expectedResult, params string[] expectedAssertion)
    {
        // Arrange
        var sut = Spec
            .From((double[] arr) => arr[0] % 1 > 0 && arr[1] % 1 > 0)
            .Create("array-access");

        // Act
        var act = sut.IsSatisfiedBy(values);

        // Assert
        act.Satisfied.ShouldBe(expectedResult);
        act.Assertions.ShouldBe(expectedAssertion);
    }

    [Theory]
    [InlineData("test", 4, true, "s.Length == 4")]
    [InlineData("test", 5, false, "s.Length != 5")]
    public void Should_evaluate_method_calls_with_out_parameters(string input, int expected, bool expectedResult, params string[] expectedAssertion)
    {
        // Arrange
        var sut = Spec
            .From((string s) => s.Length == Display.AsValue(expected))
            .Create("method-call");

        // Act
        var act = sut.IsSatisfiedBy(input);

        // Assert
        act.Satisfied.ShouldBe(expectedResult);
        act.Assertions.ShouldBe(expectedAssertion);
    }

    [Theory]
    [InlineData(new long[] { 1, 2, 3 }, true, "(int)arr[0] + (int)arr[1] + (int)arr[2] > 0")]
    [InlineData(new long[] { 0, 0, 0 }, false, "(int)arr[0] + (int)arr[1] + (int)arr[2] <= 0")]
    public void Should_evaluate_expressions_with_type_conversions(long[] input, bool expectedResult, params string[] expectedAssertion)
    {
        // Arrange
        var sut = Spec
            .From((long[] arr) => (int)arr[0] + (int)arr[1] + (int)arr[2] > 0)
            .Create("type-conversion");

        // Act
        var act = sut.IsSatisfiedBy(input);

        // Assert
        act.Satisfied.ShouldBe(expectedResult);
        act.Assertions.ShouldBe(expectedAssertion);
    }

    [Theory]
    [InlineData(42.0, true, "d == 42.0")]
    [InlineData(42.1, false, "d != 42.0")]
    public void Should_evaluate_expressions_with_constant_comparisons(decimal value, bool expectedResult, params string[] expectedAssertion)
    {
        // fyi: const identifiers are aggressively inlined by the compiler
        const decimal comparison = 42.0m;

        // Arrange
        var sut = Spec
            .From((decimal d) => d == comparison)
            .Create("constant-comparison");

        // Act
        var act = sut.IsSatisfiedBy(value);

        // Assert
        act.Satisfied.ShouldBe(expectedResult);
        act.Assertions.ShouldBe(expectedAssertion);
    }

    [Fact]
    public void Should_add_arithmetic_parentheses_based_on_precedence_rules()
    {
        // Arrange
        var sut = Spec
            .From((int n) => (1 + n) * (20 - n) / ((n + 1) * 2) / 2 > 0)
            .Create("compound-arithmetic");

        // Act
        var act = sut.IsSatisfiedBy(1);

        // Assert
        act.Assertions.ShouldBe(["(1 + n) * (20 - n) / ((n + 1) * 2) / 2 > 0"]);
    }
}
