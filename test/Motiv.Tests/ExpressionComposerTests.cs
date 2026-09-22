using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;

namespace Motiv.Tests;

public class ExpressionComposerTests
{
    private static ExpressionSpecDecorator<int, string> CreateExpressionSpec(
        Expression<Func<int, bool>> expression, string statement) =>
        new(Spec.Build(expression.Compile()).Create(statement), expression);

    [Theory]
    [InlineData(4, true)]   // even and > 3
    [InlineData(2, false)]  // even but not > 3
    [InlineData(5, false)]  // > 3 but odd
    public void Should_combine_two_expressions_with_a_shared_parameter(int model, bool expected)
    {
        // Arrange
        var left = CreateExpressionSpec(n => n % 2 == 0, "is even");
        var right = CreateExpressionSpec(n => n > 3, "is greater than three");

        // Act
        var act = ExpressionComposer.Combine(left, right, Expression.AndAlso);

        // Assert
        act.Body.NodeType.ShouldBe(ExpressionType.AndAlso);
        act.Parameters.Count.ShouldBe(1);
        act.Compile()(model).ShouldBe(expected);
    }

    [Fact]
    public void Should_rebind_the_right_operand_parameter_to_the_left_operand_parameter()
    {
        // Arrange
        var left = CreateExpressionSpec(a => a % 2 == 0, "is even");
        var right = CreateExpressionSpec(b => b > 3, "is greater than three");

        // Act
        var act = ExpressionComposer.Combine(left, right, Expression.And);

        // Assert — compilation fails if the right body still references its original parameter
        act.Compile()(4).ShouldBeTrue();
        act.Parameters[0].ShouldBeSameAs(left.ToExpression().Parameters[0]);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(3, true)]
    public void Should_negate_an_expression(int model, bool expected)
    {
        // Arrange
        var operand = CreateExpressionSpec(n => n % 2 == 0, "is even");

        // Act
        var act = ExpressionComposer.Negate(operand);

        // Assert
        act.Body.NodeType.ShouldBe(ExpressionType.Not);
        act.Compile()(model).ShouldBe(expected);
    }
}
