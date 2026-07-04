using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;

namespace Motiv.Tests;

public class ExpressionSpecNegationTests
{
    private static ExpressionSpecBase<int, string> IsEvenSpec() =>
        new ExpressionSpecDecorator<int, string>(
            Spec.Build((int n) => n % 2 == 0).Create("is even"),
            n => n % 2 == 0);

    private static ExpressionPolicyBase<int, string> IsEvenPolicy() =>
        new ExpressionPolicyDecorator<int, string>(
            Spec.Build((int n) => n % 2 == 0).WhenTrue("even").WhenFalse("odd").Create(),
            n => n % 2 == 0);

    private static ExpressionPolicyBase<int, string> IsPositivePolicy() =>
        new ExpressionPolicyDecorator<int, string>(
            Spec.Build((int n) => n > 0).WhenTrue("positive").WhenFalse("not positive").Create(),
            n => n > 0);

    [Theory]
    [InlineData(4)]
    [InlineData(3)]
    public void Should_negate_an_expression_spec_and_stay_expression_backed(int model)
    {
        // Arrange
        var sut = IsEvenSpec().Not();
        SpecBase<int, string> ordinaryOperand = IsEvenSpec();
        var ordinary = ordinaryOperand.Not();

        // Act & Assert
        sut.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        sut.ToExpression().Body.NodeType.ShouldBe(ExpressionType.Not);
        sut.ToExpression().Compile()(model).ShouldBe(sut.Matches(model));
        sut.Evaluate(model).Reason.ShouldBe(ordinary.Evaluate(model).Reason);
        sut.Evaluate(model).Justification.ShouldBe(ordinary.Evaluate(model).Justification);
    }

    [Fact]
    public void Should_preserve_policy_ness_when_negating_an_expression_policy()
    {
        // Act
        var act = IsEvenPolicy().Not();

        // Assert
        act.ShouldBeAssignableTo<ExpressionPolicyBase<int, string>>();
        act.Evaluate(3).Value.ShouldBe("odd");
        act.ToExpression().Compile()(3).ShouldBeTrue();
    }

    [Fact]
    public void Should_preserve_policy_ness_when_bang_operator_is_used()
    {
        // Act
        var act = !IsEvenPolicy();

        // Assert
        act.ShouldBeAssignableTo<ExpressionPolicyBase<int, string>>();
    }

    [Theory]
    [InlineData(4, true)]   // even → left satisfied
    [InlineData(3, true)]   // odd but positive
    [InlineData(-3, false)] // odd and not positive
    public void Should_preserve_policy_ness_when_or_else_combines_two_expression_policies(int model, bool expected)
    {
        // Arrange
        var sut = IsEvenPolicy().OrElse(IsPositivePolicy());
        PolicyBase<int, string> l = IsEvenPolicy();
        PolicyBase<int, string> r = IsPositivePolicy();
        var ordinary = l.OrElse(r);

        // Act
        var act = sut.Evaluate(model);

        // Assert
        sut.ShouldBeAssignableTo<ExpressionPolicyBase<int, string>>();
        act.Satisfied.ShouldBe(expected);
        act.Reason.ShouldBe(ordinary.Evaluate(model).Reason);
        act.Value.ShouldBe(ordinary.Evaluate(model).Value);
        sut.ToExpression().Body.NodeType.ShouldBe(ExpressionType.OrElse);
        sut.ToExpression().Compile()(model).ShouldBe(expected);
    }
}
