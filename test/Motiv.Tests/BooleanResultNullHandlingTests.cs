using Motiv.AndAlso;
using Motiv.OrElse;

namespace Motiv.Tests;

public class BooleanResultNullHandlingTests
{
    private static readonly BooleanResultBase? NullResult = null;

    private static BooleanResultBase<string> CreateResult(bool satisfied) =>
        CreatePolicyResult(satisfied);

    private static PolicyResultBase<string> CreatePolicyResult(bool satisfied) =>
        Spec
            .Build((bool model) => model)
            .Create("is satisfied")
            .Evaluate(satisfied);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Should_not_equal_a_bool_when_the_result_on_the_right_is_null(bool value)
    {
        // Act
        var act = value == NullResult;

        // Assert
        act.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Should_not_equal_a_bool_when_the_result_on_the_left_is_null(bool value)
    {
        // Act
        var act = NullResult == value;

        // Assert
        act.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Should_be_unequal_to_a_bool_when_the_result_on_the_right_is_null(bool value)
    {
        // Act
        var act = value != NullResult;

        // Assert
        act.ShouldBeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Should_be_unequal_to_a_bool_when_the_result_on_the_left_is_null(bool value)
    {
        // Act
        var act = NullResult != value;

        // Assert
        act.ShouldBeTrue();
    }

    [Fact]
    public void Should_treat_two_null_results_as_equal()
    {
        // Arrange
        BooleanResultBase? other = null;

        // Act
        var equal = NullResult == other;
        var unequal = NullResult != other;

        // Assert
        equal.ShouldBeTrue();
        unequal.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Should_not_equal_a_result_when_the_result_on_the_left_is_null(bool satisfied)
    {
        // Arrange
        var result = CreateResult(satisfied);

        // Act
        var equal = NullResult == result;
        var unequal = NullResult != result;

        // Assert
        equal.ShouldBeFalse();
        unequal.ShouldBeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Should_not_equal_a_result_when_the_result_on_the_right_is_null(bool satisfied)
    {
        // Arrange
        var result = CreateResult(satisfied);

        // Act
        var equal = result == NullResult;
        var unequal = result != NullResult;

        // Assert
        equal.ShouldBeFalse();
        unequal.ShouldBeTrue();
    }

    [Fact]
    public void Should_not_need_the_right_operand_of_AND_ALSO_when_the_left_is_unsatisfied()
    {
        // Arrange
        var left = CreateResult(false);

        // Act
        var result = new AndAlsoBooleanResult<string>(left);

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Assertions.ShouldBe(["is satisfied == false"]);
    }

    [Fact]
    public void Should_not_need_the_right_operand_of_OR_ELSE_when_the_left_is_satisfied()
    {
        // Arrange
        var left = CreateResult(true);

        // Act
        var result = new OrElseBooleanResult<string>(left);

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["is satisfied == true"]);
    }

    [Fact]
    public void Should_not_need_the_right_operand_of_an_AND_ALSO_policy_when_the_left_is_unsatisfied()
    {
        // Arrange
        var left = CreatePolicyResult(false);

        // Act
        var result = new AndAlsoPolicyResult<string>(left);

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Value.ShouldBe("is satisfied == false");
    }

    [Fact]
    public void Should_not_need_the_right_operand_of_an_OR_ELSE_policy_when_the_left_is_satisfied()
    {
        // Arrange
        var left = CreatePolicyResult(true);

        // Act
        var result = new OrElsePolicyResult<string>(left);

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Value.ShouldBe("is satisfied == true");
    }

    [Fact]
    public void Should_reject_a_missing_right_operand_of_AND_ALSO_when_the_left_is_satisfied()
    {
        // Arrange
        var left = CreateResult(true);

        // Act
        var act = () => new AndAlsoBooleanResult<string>(left);

        // Assert
        act.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe<string?>("right");
    }

    [Fact]
    public void Should_reject_a_missing_right_operand_of_OR_ELSE_when_the_left_is_unsatisfied()
    {
        // Arrange
        var left = CreateResult(false);

        // Act
        var act = () => new OrElseBooleanResult<string>(left);

        // Assert
        act.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe<string?>("right");
    }

    [Fact]
    public void Should_reject_a_missing_right_operand_of_an_AND_ALSO_policy_when_the_left_is_satisfied()
    {
        // Arrange
        var left = CreatePolicyResult(true);

        // Act
        var act = () => new AndAlsoPolicyResult<string>(left);

        // Assert
        act.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe<string?>("right");
    }

    [Fact]
    public void Should_reject_a_missing_right_operand_of_an_OR_ELSE_policy_when_the_left_is_unsatisfied()
    {
        // Arrange
        var left = CreatePolicyResult(false);

        // Act
        var act = () => new OrElsePolicyResult<string>(left);

        // Assert
        act.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe<string?>("right");
    }
}
