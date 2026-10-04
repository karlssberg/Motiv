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
        var exception = act.ShouldThrow<ArgumentNullException>();
        exception.ParamName.ShouldBe<string?>("right");
        exception.Message.ShouldContain("The right operand must be supplied when the left operand is satisfied.");
    }

    [Fact]
    public void Should_reject_a_missing_right_operand_of_OR_ELSE_when_the_left_is_unsatisfied()
    {
        // Arrange
        var left = CreateResult(false);

        // Act
        var act = () => new OrElseBooleanResult<string>(left);

        // Assert
        var exception = act.ShouldThrow<ArgumentNullException>();
        exception.ParamName.ShouldBe<string?>("right");
        exception.Message.ShouldContain("The right operand must be supplied when the left operand is unsatisfied.");
    }

    [Fact]
    public void Should_reject_a_missing_right_operand_of_an_AND_ALSO_policy_when_the_left_is_satisfied()
    {
        // Arrange
        var left = CreatePolicyResult(true);

        // Act
        var act = () => new AndAlsoPolicyResult<string>(left);

        // Assert
        var exception = act.ShouldThrow<ArgumentNullException>();
        exception.ParamName.ShouldBe<string?>("right");
        exception.Message.ShouldContain("The right operand must be supplied when the left operand is satisfied.");
    }

    [Fact]
    public void Should_reject_a_missing_right_operand_of_an_OR_ELSE_policy_when_the_left_is_unsatisfied()
    {
        // Arrange
        var left = CreatePolicyResult(false);

        // Act
        var act = () => new OrElsePolicyResult<string>(left);

        // Assert
        var exception = act.ShouldThrow<ArgumentNullException>();
        exception.ParamName.ShouldBe<string?>("right");
        exception.Message.ShouldContain("The right operand must be supplied when the left operand is unsatisfied.");
    }

    private static readonly Dictionary<string, Action<bool>> CombinatorsWithNullRight = new()
    {
        ["untyped And"] = satisfied => ((BooleanResultBase)CreateResult(satisfied)).And(null!),
        ["untyped AndAlso"] = satisfied => ((BooleanResultBase)CreateResult(satisfied)).AndAlso(null!),
        ["untyped Or"] = satisfied => ((BooleanResultBase)CreateResult(satisfied)).Or(null!),
        ["untyped OrElse"] = satisfied => ((BooleanResultBase)CreateResult(satisfied)).OrElse(null!),
        ["untyped XOr"] = satisfied => ((BooleanResultBase)CreateResult(satisfied)).XOr(null!),
        ["untyped &"] = satisfied => _ = (BooleanResultBase)CreateResult(satisfied) & (BooleanResultBase)null!,
        ["untyped |"] = satisfied => _ = (BooleanResultBase)CreateResult(satisfied) | (BooleanResultBase)null!,
        ["untyped ^"] = satisfied => _ = (BooleanResultBase)CreateResult(satisfied) ^ (BooleanResultBase)null!,
        ["typed And"] = satisfied => CreateResult(satisfied).And(null!),
        ["typed AndAlso"] = satisfied => CreateResult(satisfied).AndAlso(null!),
        ["typed Or"] = satisfied => CreateResult(satisfied).Or(null!),
        ["typed OrElse"] = satisfied => CreateResult(satisfied).OrElse(null!),
        ["typed XOr"] = satisfied => CreateResult(satisfied).XOr(null!),
        ["typed &"] = satisfied => _ = CreateResult(satisfied) & (BooleanResultBase<string>)null!,
        ["typed |"] = satisfied => _ = CreateResult(satisfied) | (BooleanResultBase<string>)null!,
        ["typed ^"] = satisfied => _ = CreateResult(satisfied) ^ (BooleanResultBase<string>)null!,
        ["policy AndAlso"] = satisfied => CreatePolicyResult(satisfied).AndAlso(null!),
        ["policy OrElse"] = satisfied => CreatePolicyResult(satisfied).OrElse(null!),
    };

    private static readonly Dictionary<string, Action<bool>> CombinatorsWithNullLeft = new()
    {
        ["untyped &"] = satisfied => _ = (BooleanResultBase)null! & (BooleanResultBase)CreateResult(satisfied),
        ["untyped |"] = satisfied => _ = (BooleanResultBase)null! | (BooleanResultBase)CreateResult(satisfied),
        ["untyped ^"] = satisfied => _ = (BooleanResultBase)null! ^ (BooleanResultBase)CreateResult(satisfied),
        ["typed &"] = satisfied => _ = (BooleanResultBase<string>)null! & CreateResult(satisfied),
        ["typed |"] = satisfied => _ = (BooleanResultBase<string>)null! | CreateResult(satisfied),
        ["typed ^"] = satisfied => _ = (BooleanResultBase<string>)null! ^ CreateResult(satisfied),
    };

    public static TheoryData<string, bool> NullRightCases() => CasesFor(CombinatorsWithNullRight);

    public static TheoryData<string, bool> NullLeftCases() => CasesFor(CombinatorsWithNullLeft);

    private static TheoryData<string, bool> CasesFor(Dictionary<string, Action<bool>> combinators)
    {
        var data = new TheoryData<string, bool>();
        foreach (var name in combinators.Keys)
        {
            data.Add(name, true);
            data.Add(name, false);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(NullRightCases))]
    public void Should_reject_a_null_right_operand_whatever_the_left_operand(string combinator, bool satisfied)
    {
        // Act
        var act = () => CombinatorsWithNullRight[combinator](satisfied);

        // Assert
        var exception = act.ShouldThrow<ArgumentNullException>();
        exception.ParamName.ShouldBe<string?>("right");
        exception.Message.ShouldContain("'right' cannot be null");
    }

    [Theory]
    [MemberData(nameof(NullLeftCases))]
    public void Should_reject_a_null_left_operand(string combinator, bool satisfied)
    {
        // Act
        var act = () => CombinatorsWithNullLeft[combinator](satisfied);

        // Assert
        var exception = act.ShouldThrow<ArgumentNullException>();
        exception.ParamName.ShouldBe<string?>("left");
        exception.Message.ShouldContain("'left' cannot be null");
    }

    private static BooleanResultBase CreateUntypedResult(string name, bool satisfied) =>
        Spec
            .Build((bool model) => model)
            .Create(name)
            .Evaluate(satisfied);

    [Theory]
    [InlineData(true, true, true, new[] { "left == true", "right == true" })]
    [InlineData(true, false, false, new[] { "right == false" })]
    [InlineData(false, true, false, new[] { "left == false" })]
    [InlineData(false, false, false, new[] { "left == false" })]
    public void Should_short_circuit_an_untyped_AND_ALSO_with_a_non_null_right_operand(
        bool leftSatisfied,
        bool rightSatisfied,
        bool expectedSatisfied,
        string[] expectedAssertions)
    {
        // Arrange
        var left = CreateUntypedResult("left", leftSatisfied);
        var right = CreateUntypedResult("right", rightSatisfied);

        // Act
        var result = left.AndAlso(right);

        // Assert
        result.Satisfied.ShouldBe(expectedSatisfied);
        result.Assertions.ShouldBe(expectedAssertions);
    }

    [Theory]
    [InlineData(true, true, true, new[] { "left == true" })]
    [InlineData(true, false, true, new[] { "left == true" })]
    [InlineData(false, true, true, new[] { "right == true" })]
    [InlineData(false, false, false, new[] { "left == false", "right == false" })]
    public void Should_short_circuit_an_untyped_OR_ELSE_with_a_non_null_right_operand(
        bool leftSatisfied,
        bool rightSatisfied,
        bool expectedSatisfied,
        string[] expectedAssertions)
    {
        // Arrange
        var left = CreateUntypedResult("left", leftSatisfied);
        var right = CreateUntypedResult("right", rightSatisfied);

        // Act
        var result = left.OrElse(right);

        // Assert
        result.Satisfied.ShouldBe(expectedSatisfied);
        result.Assertions.ShouldBe(expectedAssertions);
    }
}
