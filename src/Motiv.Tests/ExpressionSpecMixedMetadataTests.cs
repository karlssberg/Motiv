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

    private static ExpressionPolicyBase<int, string> IsPositivePolicy() =>
        new ExpressionPolicyDecorator<int, string>(
            Spec.Build((int n) => n > 0).WhenTrue("is positive").WhenFalse("is not positive").Create(),
            n => n > 0);

    private static PolicyBase<int, string> FallbackPolicy() =>
        Spec.Build((int n) => n > 100).WhenTrue("is large").WhenFalse("is not large").Create();

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

    [Theory]
    [InlineData(4)]
    [InlineData(-3)]
    public void Should_preserve_policy_else_semantics_when_or_elseing_with_an_ordinary_policy(int model)
    {
        // Arrange — the alternative is statically a PolicyBase, so the Policy-Else combinator (which
        // returns a PolicyBase) must be preferred over the boolean-only OrElse degrade path
        var expressionPolicy = IsPositivePolicy();
        PolicyBase<int, string> ordinaryPolicy = FallbackPolicy();
        PolicyBase<int, string> expressionPolicyAsBasePolicy = expressionPolicy;
        var expected = expressionPolicyAsBasePolicy.OrElse(ordinaryPolicy);

        // Act
        var act = expressionPolicy.OrElse(ordinaryPolicy);

        // Assert
        act.ShouldBeAssignableTo<PolicyBase<int, string>>();
        act.Evaluate(model).Value.ShouldBe(expected.Evaluate(model).Value);
        act.Evaluate(model).Reason.ShouldBe(expected.Evaluate(model).Reason);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(-3)]
    public void Should_degrade_to_an_ordinary_spec_when_and_alsoing_with_a_non_expression_spec(int model)
    {
        // Arrange
        var exprSpec = IsPositive();
        var ordinary = Spec.Build((int n) => n < 100).Create("is small");
        SpecBase<int, string> ordinaryExprSpec = exprSpec;
        var expected = ordinaryExprSpec.AndAlso(ordinary);

        // Act
        var act = exprSpec.AndAlso(ordinary);

        // Assert
        (act is IExpressionSpec<int>).ShouldBeFalse();
        act.Matches(model).ShouldBe(expected.Matches(model));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(-3)]
    public void Should_degrade_to_an_ordinary_spec_when_oring_with_a_non_expression_spec(int model)
    {
        // Arrange
        var exprSpec = IsPositive();
        var ordinary = Spec.Build((int n) => n < 100).Create("is small");
        SpecBase<int, string> ordinaryExprSpec = exprSpec;
        var expected = ordinaryExprSpec.Or(ordinary);

        // Act
        var act = exprSpec.Or(ordinary);

        // Assert
        (act is IExpressionSpec<int>).ShouldBeFalse();
        act.Matches(model).ShouldBe(expected.Matches(model));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(-3)]
    public void Should_degrade_to_an_ordinary_spec_when_or_elseing_with_a_non_expression_spec(int model)
    {
        // Arrange
        var exprSpec = IsPositive();
        var ordinary = Spec.Build((int n) => n < 100).Create("is small");
        SpecBase<int, string> ordinaryExprSpec = exprSpec;
        var expected = ordinaryExprSpec.OrElse(ordinary);

        // Act
        var act = exprSpec.OrElse(ordinary);

        // Assert
        (act is IExpressionSpec<int>).ShouldBeFalse();
        act.Matches(model).ShouldBe(expected.Matches(model));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(-3)]
    public void Should_degrade_to_an_ordinary_spec_when_xoring_with_a_non_expression_spec(int model)
    {
        // Arrange
        var exprSpec = IsPositive();
        var ordinary = Spec.Build((int n) => n < 100).Create("is small");
        SpecBase<int, string> ordinaryExprSpec = exprSpec;
        var expected = ordinaryExprSpec.XOr(ordinary);

        // Act
        var act = exprSpec.XOr(ordinary);

        // Assert
        (act is IExpressionSpec<int>).ShouldBeFalse();
        act.Matches(model).ShouldBe(expected.Matches(model));
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(-3, false)]
    public void Should_compose_with_a_different_metadata_ordinary_spec_via_the_untyped_overload(int model, bool expectedMatch)
    {
        // Arrange — the ordinary operand's metadata type (Guid) differs from the expression spec's
        // (string), ruling out both the same-metadata overload and the generic expression-spec overload
        // (Guid-metadata policies don't implement IExpressionSpec<TModel>), so this must bind to the
        // untyped SpecBase<TModel>.And(SpecBase<TModel>) overload and still compose correctly.
        var exprSpec = IsPositive();
        var ordinaryGuidSpec = Spec.Build((int n) => n > 0)
            .WhenTrue(Guid.NewGuid())
            .WhenFalse(Guid.NewGuid())
            .Create("positive");

        // Act
        var act = exprSpec.And(ordinaryGuidSpec);

        // Assert
        act.ShouldBeAssignableTo<SpecBase<int, string>>();
        act.Matches(model).ShouldBe(expectedMatch);
    }
}
