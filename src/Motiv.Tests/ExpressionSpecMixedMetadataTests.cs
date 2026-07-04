using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;

namespace Motiv.Tests;

public class ExpressionSpecMixedMetadataTests
{
    private static ExpressionPolicyBase<int, int> ErrorCodeSpec() =>
        new ExpressionPolicyDecorator<int, int>(
            Spec.Build((int n) => n % 2 == 0).WhenTrue(0).WhenFalse(422).Create("is even"),
            n => n % 2 == 0);

    private static ExpressionSpecBase<int, string> IsPositive() =>
        new ExpressionSpecDecorator<int, string>(
            Spec.Build((int n) => n > 0).Create("is positive"),
            n => n > 0);

    [Theory]
    [InlineData(4)]
    [InlineData(-3)]
    public void Should_stay_expression_backed_when_combining_specs_with_different_metadata(int model)
    {
        // Arrange
        var sut = IsPositive().And(ErrorCodeSpec());
        SpecBase<int> l = IsPositive();
        SpecBase<int> r = ErrorCodeSpec();
        var ordinary = l.And(r);

        // Act & Assert
        sut.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        sut.ToExpression().Compile()(model).ShouldBe(sut.Matches(model));
        sut.Evaluate(model).Reason.ShouldBe(ordinary.Evaluate(model).Reason);
        sut.Evaluate(model).Assertions.ShouldBe(ordinary.Evaluate(model).Assertions);
    }

    [Fact]
    public void Should_prefer_the_same_metadata_overload_when_metadata_types_match()
    {
        // Arrange
        var left = IsPositive();
        var right = new ExpressionSpecDecorator<int, string>(
            Spec.Build((int n) => n < 100).Create("is small"),
            n => n < 100);

        // Act — same metadata must NOT coerce to string via the generic overload
        ExpressionSpecBase<int, string> act = left.And(right);

        // Assert
        act.ToExpression().Body.NodeType.ShouldBe(ExpressionType.And);
    }

    [Fact]
    public void Should_preserve_expression_when_explicitly_coerced_to_explanation_spec()
    {
        // Act
        var act = ErrorCodeSpec().ToExplanationSpec();

        // Assert
        act.ShouldBeAssignableTo<IExpressionSpec<int>>();
        ((IExpressionSpec<int>)act).ToExpression().Compile()(4).ShouldBeTrue();
    }
}
