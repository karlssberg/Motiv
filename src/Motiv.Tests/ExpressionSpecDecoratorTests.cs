using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;

namespace Motiv.Tests;

public class ExpressionSpecDecoratorTests
{
    [Fact]
    public void Should_return_the_same_expression_instance_that_was_supplied()
    {
        // Arrange
        Expression<Func<int, bool>> expression = n => n > 3;
        var inner = Spec.Build((int n) => n > 3).Create("is greater than three");
        var sut = new ExpressionSpecDecorator<int, string>(inner, expression);

        // Act
        var act = sut.ToExpression();

        // Assert
        act.ShouldBeSameAs(expression);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(4, true)]
    public void Should_forward_evaluation_to_the_underlying_spec(int model, bool expected)
    {
        // Arrange
        Expression<Func<int, bool>> expression = n => n > 3;
        var inner = Spec.Build((int n) => n > 3).Create("is greater than three");
        var sut = new ExpressionSpecDecorator<int, string>(inner, expression);

        // Act
        var act = sut.Evaluate(model);

        // Assert
        act.Satisfied.ShouldBe(expected);
        act.Reason.ShouldBe(inner.Evaluate(model).Reason);
        act.Assertions.ShouldBe(inner.Evaluate(model).Assertions);
    }

    [Fact]
    public void Should_forward_description_and_underlying_to_the_underlying_spec()
    {
        // Arrange
        Expression<Func<int, bool>> expression = n => n > 3;
        var inner = Spec.Build((int n) => n > 3).Create("is greater than three");
        var sut = new ExpressionSpecDecorator<int, string>(inner, expression);

        // Act & Assert
        sut.Description.ShouldBeSameAs(inner.Description);
        sut.Underlying.ShouldBe(inner.Underlying);
        sut.Matches(4).ShouldBeTrue();
    }

    [Fact]
    public void Should_be_assignable_to_both_spec_base_and_expression_spec()
    {
        // Arrange
        Expression<Func<int, bool>> expression = n => n > 3;
        var inner = Spec.Build((int n) => n > 3).Create("is greater than three");

        // Act
        var sut = new ExpressionSpecDecorator<int, string>(inner, expression);

        // Assert
        sut.ShouldBeAssignableTo<SpecBase<int, string>>();
        sut.ShouldBeAssignableTo<IExpressionSpec<int>>();
        sut.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
    }
}
