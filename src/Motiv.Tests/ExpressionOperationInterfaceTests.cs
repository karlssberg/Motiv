using Motiv.ExpressionTreeProposition;
using Motiv.Not;
using Motiv.Traversal;

namespace Motiv.Tests;

public class ExpressionOperationInterfaceTests
{
    private static ExpressionSpecBase<int, string> IsEven() =>
        new ExpressionSpecDecorator<int, string>(
            Spec.Build((int n) => n % 2 == 0).Create("is even"),
            n => n % 2 == 0);

    private static ExpressionSpecBase<int, string> IsPositive() =>
        new ExpressionSpecDecorator<int, string>(
            Spec.Build((int n) => n > 0).Create("is positive"),
            n => n > 0);

    private static ExpressionPolicyBase<int, string> IsEvenPolicy() =>
        new ExpressionPolicyDecorator<int, string>(
            Spec.Build((int n) => n % 2 == 0).WhenTrue("is even").WhenFalse("is odd").Create(),
            n => n % 2 == 0);

    private static ExpressionPolicyBase<int, string> IsPositivePolicy() =>
        new ExpressionPolicyDecorator<int, string>(
            Spec.Build((int n) => n > 0).WhenTrue("is positive").WhenFalse("is not positive").Create(),
            n => n > 0);

    public static IEnumerable<object[]> OrdinaryBinarySpecs()
    {
        SpecBase<int, string> left = IsEven();
        SpecBase<int, string> right = IsPositive();

        yield return [left.And(right), Operator.And, true];
        yield return [left.AndAlso(right), Operator.AndAlso, true];
        yield return [left.Or(right), Operator.Or, true];
        yield return [left.OrElse(right), Operator.OrElse, true];
        yield return [left.XOr(right), Operator.XOr, false];
    }

    public static IEnumerable<object[]> ExpressionBinarySpecs()
    {
        var left = IsEven();
        var right = IsPositive();

        yield return [left.And(right), Operator.And, true];
        yield return [left.AndAlso(right), Operator.AndAlso, true];
        yield return [left.Or(right), Operator.Or, true];
        yield return [left.OrElse(right), Operator.OrElse, true];
        yield return [left.XOr(right), Operator.XOr, false];
    }

    [Theory]
    [MemberData(nameof(OrdinaryBinarySpecs))]
    [MemberData(nameof(ExpressionBinarySpecs))]
    public void Should_expose_left_and_right_through_every_binary_operation_interface_level(
        SpecBase<int, string> sut, string expectedOperation, bool expectedIsCollapsable)
    {
        // Act
        var typed = sut.ShouldBeAssignableTo<IBinaryOperationSpec<int, string>>()!;
        var modelTyped = sut.ShouldBeAssignableTo<IBinaryOperationSpec<int>>()!;
        var untyped = sut.ShouldBeAssignableTo<IBinaryOperationSpec>()!;

        // Assert
        typed.Operation.ShouldBe(expectedOperation);
        typed.IsCollapsable.ShouldBe(expectedIsCollapsable);
        ((object)modelTyped.Left).ShouldBeSameAs(typed.Left);
        ((object)modelTyped.Right).ShouldBeSameAs(typed.Right);
        ((object)untyped.Left).ShouldBeSameAs(typed.Left);
        ((object)untyped.Right).ShouldBeSameAs(typed.Right);
        sut.Underlying.ShouldBe([typed.Left, typed.Right]);
    }

    [Fact]
    public void Should_expose_the_policy_specific_left_and_right_on_or_else_policy()
    {
        // Arrange
        PolicyBase<int, string> left = Spec.Build((int n) => n % 2 == 0).WhenTrue("is even").WhenFalse("is odd").Create();
        PolicyBase<int, string> right = Spec.Build((int n) => n > 0).WhenTrue("is positive").WhenFalse("is not positive").Create();

        // Act
        var sut = left.OrElse(right);
        var typed = sut.ShouldBeAssignableTo<IBinaryOperationSpec<int, string>>()!;
        var modelTyped = sut.ShouldBeAssignableTo<IBinaryOperationSpec<int>>()!;
        var untyped = sut.ShouldBeAssignableTo<IBinaryOperationSpec>()!;

        // Assert
        typed.Operation.ShouldBe(Operator.OrElse);
        typed.IsCollapsable.ShouldBeTrue();
        ((object)typed.Left).ShouldBeSameAs(left);
        ((object)typed.Right).ShouldBeSameAs(right);
        ((object)modelTyped.Left).ShouldBeSameAs(left);
        ((object)modelTyped.Right).ShouldBeSameAs(right);
        ((object)untyped.Left).ShouldBeSameAs(left);
        ((object)untyped.Right).ShouldBeSameAs(right);
        sut.Underlying.ShouldBe([left, right]);
        sut.Description.ShouldNotBeNull();
        sut.Matches(4).ShouldBeTrue();
    }

    [Fact]
    public void Should_expose_the_policy_specific_left_and_right_on_expression_or_else_policy()
    {
        // Arrange
        var left = IsEvenPolicy();
        var right = IsPositivePolicy();

        // Act
        var sut = left.OrElse(right);
        var typed = sut.ShouldBeAssignableTo<IBinaryOperationSpec<int, string>>()!;
        var modelTyped = sut.ShouldBeAssignableTo<IBinaryOperationSpec<int>>()!;
        var untyped = sut.ShouldBeAssignableTo<IBinaryOperationSpec>()!;

        // Assert
        typed.Operation.ShouldBe(Operator.OrElse);
        typed.IsCollapsable.ShouldBeTrue();
        ((object)typed.Left).ShouldBeSameAs(left);
        ((object)typed.Right).ShouldBeSameAs(right);
        ((object)modelTyped.Left).ShouldBeSameAs(left);
        ((object)modelTyped.Right).ShouldBeSameAs(right);
        ((object)untyped.Left).ShouldBeSameAs(left);
        ((object)untyped.Right).ShouldBeSameAs(right);
        sut.Underlying.ShouldBe([left, right]);
        sut.Description.ShouldNotBeNull();
        sut.Matches(4).ShouldBeTrue();
    }

    [Fact]
    public void Should_expose_the_operand_through_every_unary_operation_interface_level_for_expression_not_spec()
    {
        // Arrange
        var operand = IsEven();

        // Act
        var sut = operand.Not();
        var typed = sut.ShouldBeAssignableTo<IUnaryOperationSpec<int, string>>()!;
        var modelTyped = sut.ShouldBeAssignableTo<IUnaryOperationSpec<int>>()!;
        var untyped = sut.ShouldBeAssignableTo<IUnaryOperationSpec>()!;

        // Assert
        typed.Operation.ShouldBe(Operator.Not);
        typed.IsCollapsable.ShouldBeFalse();
        ((object)typed.Operand).ShouldBeSameAs(operand);
        ((object)modelTyped.Operand).ShouldBeSameAs(operand);
        ((object)untyped.Operand).ShouldBeSameAs(operand);
        sut.Description.ShouldNotBeNull();
        sut.Underlying.ShouldBe([operand]);
        ((object)((ExpressionNotSpec<int, string>)sut).Operand).ShouldBeSameAs(operand);
    }

    [Fact]
    public void Should_expose_the_operand_through_every_unary_operation_interface_level_for_expression_not_policy()
    {
        // Arrange
        var operand = IsEvenPolicy();

        // Act
        var sut = operand.Not();
        var typed = sut.ShouldBeAssignableTo<IUnaryOperationSpec<int, string>>()!;
        var modelTyped = sut.ShouldBeAssignableTo<IUnaryOperationSpec<int>>()!;
        var untyped = sut.ShouldBeAssignableTo<IUnaryOperationSpec>()!;

        // Assert
        typed.Operation.ShouldBe(Operator.Not);
        typed.IsCollapsable.ShouldBeFalse();
        ((object)typed.Operand).ShouldBeSameAs(operand);
        ((object)modelTyped.Operand).ShouldBeSameAs(operand);
        ((object)untyped.Operand).ShouldBeSameAs(operand);
        sut.Description.ShouldNotBeNull();
        sut.Underlying.ShouldBe([operand]);
        ((object)((ExpressionNotPolicy<int, string>)sut).Operand).ShouldBeSameAs(operand);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(3)]
    public void Should_evaluate_and_match_identically_for_expression_not_policy(int model)
    {
        // Arrange
        var operand = IsEvenPolicy();
        var sut = operand.Not();

        // Act
        var matches = sut.Matches(model);
        var expression = sut.ToExpression().Compile()(model);

        // Assert
        matches.ShouldBe(!operand.Matches(model));
        expression.ShouldBe(matches);
        sut.Evaluate(model).Satisfied.ShouldBe(matches);
    }
}
