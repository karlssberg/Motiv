using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;

namespace Motiv.Tests;

public class ExpressionPolicyDecoratorTests
{
    [Fact]
    public void Should_return_the_same_expression_instance_that_was_supplied()
    {
        // Arrange
        Expression<Func<int, bool>> expression = n => n > 3;
        var inner = Spec.Build((int n) => n > 3)
            .WhenTrue("is greater than three")
            .WhenFalse("is not greater than three")
            .Create();
        var sut = new ExpressionPolicyDecorator<int, string>(inner, expression);

        // Act
        var act = sut.ToExpression();

        // Assert
        act.ShouldBeSameAs(expression);
    }

    [Theory]
    [InlineData(2, false, "is not greater than three")]
    [InlineData(4, true, "is greater than three")]
    public void Should_forward_policy_evaluation_to_the_underlying_policy(int model, bool expected, string expectedAssertion)
    {
        // Arrange
        Expression<Func<int, bool>> expression = n => n > 3;
        var inner = Spec.Build((int n) => n > 3)
            .WhenTrue("is greater than three")
            .WhenFalse("is not greater than three")
            .Create();
        var sut = new ExpressionPolicyDecorator<int, string>(inner, expression);

        // Act
        var act = sut.Evaluate(model);

        // Assert
        act.Satisfied.ShouldBe(expected);
        act.Value.ShouldBe(expectedAssertion);
        act.Reason.ShouldBe(inner.Evaluate(model).Reason);
    }

    [Fact]
    public void Should_be_assignable_to_policy_base_and_expression_spec()
    {
        // Arrange
        Expression<Func<int, bool>> expression = n => n > 3;
        var inner = Spec.Build((int n) => n > 3)
            .WhenTrue("is greater than three")
            .WhenFalse("is not greater than three")
            .Create();

        // Act
        var sut = new ExpressionPolicyDecorator<int, string>(inner, expression);

        // Assert
        sut.ShouldBeAssignableTo<PolicyBase<int, string>>();
        sut.ShouldBeAssignableTo<IExpressionSpec<int>>();
        sut.ShouldBeAssignableTo<ExpressionPolicyBase<int, string>>();
    }

    [Fact]
    public void Should_forward_underlying_to_the_underlying_policy()
    {
        // Arrange
        Expression<Func<int, bool>> expression = n => n > 3;
        var inner = Spec.Build((int n) => n > 3)
            .WhenTrue("is greater than three")
            .WhenFalse("is not greater than three")
            .Create();
        var sut = new ExpressionPolicyDecorator<int, string>(inner, expression);

        // Act
        var act = sut.Underlying;

        // Assert
        act.ShouldBeSameAs(inner.Underlying);
    }
}
