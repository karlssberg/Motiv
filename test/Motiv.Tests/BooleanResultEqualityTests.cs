namespace Motiv.Tests;

public class BooleanResultEqualityTests
{
    private static BooleanResultBase<string> CreateResult(bool satisfied) =>
        Spec
            .Build((bool model) => model)
            .Create("is satisfied")
            .Evaluate(satisfied);

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Should_compare_a_result_with_a_bool_using_the_equality_operator(
        bool satisfied,
        bool value,
        bool expected)
    {
        // Arrange
        var result = CreateResult(satisfied);

        // Act
        var act = result == value;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Should_compare_a_bool_with_a_result_using_the_equality_operator(
        bool satisfied,
        bool value,
        bool expected)
    {
        // Arrange
        var result = CreateResult(satisfied);

        // Act
        var act = value == result;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void Should_compare_a_result_with_a_bool_using_the_inequality_operator(
        bool satisfied,
        bool value,
        bool expected)
    {
        // Arrange
        var result = CreateResult(satisfied);

        // Act
        var act = result != value;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void Should_compare_a_bool_with_a_result_using_the_inequality_operator(
        bool satisfied,
        bool value,
        bool expected)
    {
        // Arrange
        var result = CreateResult(satisfied);

        // Act
        var act = value != result;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Should_compare_two_results_using_the_equality_operator(
        bool leftSatisfied,
        bool rightSatisfied,
        bool expected)
    {
        // Arrange
        BooleanResultBase left = CreateResult(leftSatisfied);
        BooleanResultBase right = CreateResult(rightSatisfied);

        // Act
        var act = left == right;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void Should_compare_two_results_using_the_inequality_operator(
        bool leftSatisfied,
        bool rightSatisfied,
        bool expected)
    {
        // Arrange
        BooleanResultBase left = CreateResult(leftSatisfied);
        BooleanResultBase right = CreateResult(rightSatisfied);

        // Act
        var act = left != right;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Should_compare_a_result_with_a_bool_using_equals(
        bool satisfied,
        bool value,
        bool expected)
    {
        // Arrange
        var result = CreateResult(satisfied);

        // Act
        var act = result.Equals(value);

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Should_compare_a_result_with_a_boxed_bool_using_object_equals(
        bool satisfied,
        bool value,
        bool expected)
    {
        // Arrange
        var result = CreateResult(satisfied);

        // Act
        var act = result.Equals((object)value);

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Should_compare_a_result_with_a_boxed_result_using_object_equals(
        bool leftSatisfied,
        bool rightSatisfied,
        bool expected)
    {
        // Arrange
        var left = CreateResult(leftSatisfied);
        var right = CreateResult(rightSatisfied);

        // Act
        var act = left.Equals((object)right);

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Should_not_equal_a_null_result(bool satisfied)
    {
        // Arrange
        var result = CreateResult(satisfied);

        // Act
        var act = result.Equals((BooleanResultBase?)null);

        // Assert
        act.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Should_not_equal_a_null_object(bool satisfied)
    {
        // Arrange
        var result = CreateResult(satisfied);

        // Act
        var act = result.Equals((object?)null);

        // Assert
        act.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Should_not_equal_an_object_that_is_neither_a_bool_nor_a_result(bool satisfied)
    {
        // Arrange
        var result = CreateResult(satisfied);

        // Act
        var act = result.Equals((object)satisfied.ToString());

        // Assert
        act.ShouldBeFalse();
    }
}
