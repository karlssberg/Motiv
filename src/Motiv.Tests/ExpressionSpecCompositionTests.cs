using System.Linq.Expressions;

namespace Motiv.Tests;

public class ExpressionSpecCompositionTests
{
    private static readonly ExpressionSpecBase<int, string> IsEven =
        new Motiv.ExpressionTreeProposition.ExpressionSpecDecorator<int, string>(
            Spec.Build((int n) => n % 2 == 0).Create("is even"),
            n => n % 2 == 0);

    private static readonly ExpressionSpecBase<int, string> IsPositive =
        new Motiv.ExpressionTreeProposition.ExpressionSpecDecorator<int, string>(
            Spec.Build((int n) => n > 0).Create("is positive"),
            n => n > 0);

    [Fact]
    public void Should_produce_an_expression_spec_when_anding_two_expression_specs()
    {
        // Act
        var act = IsEven.And(IsPositive);

        // Assert
        act.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        act.ToExpression().Body.NodeType.ShouldBe(ExpressionType.And);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(3)]
    [InlineData(-2)]
    [InlineData(-3)]
    public void Should_compile_an_and_expression_that_agrees_with_spec_evaluation(int model)
    {
        // Arrange
        var sut = IsEven.And(IsPositive);

        // Act
        var act = sut.ToExpression().Compile()(model);

        // Assert
        act.ShouldBe(sut.Matches(model));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(3)]
    [InlineData(-2)]
    [InlineData(-3)]
    public void Should_produce_identical_explanations_to_ordinary_and_composition(int model)
    {
        // Arrange — upcasting forces the ordinary SpecBase overload to bind
        SpecBase<int, string> ordinaryLeft = IsEven;
        SpecBase<int, string> ordinaryRight = IsPositive;
        var ordinary = ordinaryLeft.And(ordinaryRight);
        var sut = IsEven.And(IsPositive);

        // Act
        var act = sut.Evaluate(model);
        var expected = ordinary.Evaluate(model);

        // Assert
        act.Reason.ShouldBe(expected.Reason);
        act.Assertions.ShouldBe(expected.Assertions);
        act.Justification.ShouldBe(expected.Justification);
    }

    [Fact]
    public void Should_memoize_the_composed_expression()
    {
        // Arrange
        var sut = IsEven.And(IsPositive);

        // Act & Assert
        sut.ToExpression().ShouldBeSameAs(sut.ToExpression());
    }

    [Fact]
    public void Should_degrade_to_an_ordinary_spec_when_anding_with_a_non_expression_spec()
    {
        // Arrange
        var ordinary = Spec.Build((int n) => n > 100).Create("is large");

        // Act
        var act = IsEven.And(ordinary);

        // Assert
        (act is IExpressionSpec<int>).ShouldBeFalse();
    }

    [Fact]
    public void Should_produce_an_expression_spec_when_using_the_amp_operator()
    {
        // Act
        var act = IsEven & IsPositive;

        // Assert
        act.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        act.ToExpression().Compile()(4).ShouldBeTrue();
    }

    [Fact]
    public void Should_collapse_mixed_expression_and_ordinary_nesting_identically_to_ordinary_specs()
    {
        // Arrange — an ordinary AndSpec whose left operand is an ExpressionAndSpec (mixed nesting)
        var ordinary = Spec.Build((int n) => n < 100).Create("is small");
        var mixed = IsEven.And(IsPositive).And(ordinary);

        SpecBase<int, string> l = IsEven;
        SpecBase<int, string> r = IsPositive;
        var allOrdinary = l.And(r).And(ordinary);

        // Act & Assert
        mixed.Evaluate(4).Reason.ShouldBe(allOrdinary.Evaluate(4).Reason);
        mixed.Evaluate(4).Justification.ShouldBe(allOrdinary.Evaluate(4).Justification);
        mixed.Evaluate(-3).Reason.ShouldBe(allOrdinary.Evaluate(-3).Reason);
        mixed.Evaluate(-3).Justification.ShouldBe(allOrdinary.Evaluate(-3).Justification);
    }
}
