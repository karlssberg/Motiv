using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;

namespace Motiv.Tests;

/// <summary>
/// Pins existing expression-composition behavior that isn't covered by <see cref="ExpressionSpecCompositionTests"/>.
/// These tests must pass without any production-code changes — a failure here indicates a genuine regression,
/// not a test to be "fixed" by relaxing the assertion.
/// </summary>
public class ExpressionSpecRegressionPinTests
{
    private static readonly ExpressionSpecBase<int, string> IsEven =
        new ExpressionSpecDecorator<int, string>(
            Spec.Build((int n) => n % 2 == 0).Create("is even"),
            n => n % 2 == 0);

    private static readonly ExpressionSpecBase<int, string> IsPositive =
        new ExpressionSpecDecorator<int, string>(
            Spec.Build((int n) => n > 0).Create("is positive"),
            n => n > 0);

    private static readonly ExpressionSpecBase<int, string> IsSmall =
        new ExpressionSpecDecorator<int, string>(
            Spec.Build((int n) => n < 100).Create("is small"),
            n => n < 100);

    [Fact]
    public void Should_collapse_three_way_or_nesting_identically_to_ordinary_specs()
    {
        // Arrange — upcasting forces the ordinary SpecBase overload to bind
        SpecBase<int, string> l = IsEven;
        SpecBase<int, string> m = IsPositive;
        SpecBase<int, string> r = IsSmall;
        var ordinary = l.Or(m).Or(r);
        var sut = IsEven.Or(IsPositive).Or(IsSmall);

        // Act & Assert
        sut.Evaluate(4).Reason.ShouldBe(ordinary.Evaluate(4).Reason);
        sut.Evaluate(4).Justification.ShouldBe(ordinary.Evaluate(4).Justification);
        sut.Evaluate(-3).Reason.ShouldBe(ordinary.Evaluate(-3).Reason);
        sut.Evaluate(-3).Justification.ShouldBe(ordinary.Evaluate(-3).Justification);
    }

    private static readonly ExpressionPolicyBase<int, string> IsRare =
        new ExpressionPolicyDecorator<int, string>(
            Spec.Build((int n) => n > 100).Create("is rare"),
            n => n > 100);

    private static readonly ExpressionPolicyBase<int, string> IsNegative =
        new ExpressionPolicyDecorator<int, string>(
            Spec.Build((int n) => n < 0).Create("is negative"),
            n => n < 0);

    private static readonly ExpressionPolicyBase<int, string> IsEvenPolicy =
        new ExpressionPolicyDecorator<int, string>(
            Spec.Build((int n) => n % 2 == 0).Create("is even"),
            n => n % 2 == 0);

    [Theory]
    [InlineData(4)]
    [InlineData(1)]
    [InlineData(-2)]
    public void Should_keep_or_else_policy_chain_as_expression_policy_with_ordinary_parity(int model)
    {
        // Act — static typing is the assertion: the chain must remain an ExpressionPolicyBase
        ExpressionPolicyBase<int, string> sut = IsRare.OrElse(IsNegative).OrElse(IsEvenPolicy);

        PolicyBase<int, string> l = IsRare;
        PolicyBase<int, string> m = IsNegative;
        PolicyBase<int, string> r = IsEvenPolicy;
        var ordinary = l.OrElse(m).OrElse(r);

        // Assert
        sut.Evaluate(model).Reason.ShouldBe(ordinary.Evaluate(model).Reason);
        sut.Evaluate(model).Justification.ShouldBe(ordinary.Evaluate(model).Justification);
    }

    private static readonly ExpressionSpecBase<int, string> SpecOperand =
        new ExpressionSpecDecorator<int, string>(
            Spec.Build((int n) => n % 2 == 0).Create("is even"),
            n => n % 2 == 0);

    private static readonly ExpressionPolicyBase<int, string> PolicyOperand =
        new ExpressionPolicyDecorator<int, string>(
            Spec.Build((int n) => n > 3).Create("is big"),
            n => n > 3);

    [Fact]
    public void Should_compile_and_agree_with_matches_for_and_with_spec_first()
    {
        // Act — static typing is the assertion: this must compile and remain an ExpressionSpecBase
        ExpressionSpecBase<int, string> sut = SpecOperand & PolicyOperand;

        // Assert
        sut.ToExpression().Compile()(4).ShouldBe(sut.Matches(4));
        sut.ToExpression().Compile()(1).ShouldBe(sut.Matches(1));
    }

    [Fact]
    public void Should_compile_and_agree_with_matches_for_and_with_policy_first()
    {
        // Act — static typing is the assertion: this must compile and remain an ExpressionSpecBase
        ExpressionSpecBase<int, string> sut = PolicyOperand & SpecOperand;

        // Assert
        sut.ToExpression().Compile()(4).ShouldBe(sut.Matches(4));
        sut.ToExpression().Compile()(1).ShouldBe(sut.Matches(1));
    }

    [Fact]
    public void Should_compile_and_agree_with_matches_for_or_with_spec_first()
    {
        // Act — static typing is the assertion: this must compile and remain an ExpressionSpecBase
        ExpressionSpecBase<int, string> sut = SpecOperand | PolicyOperand;

        // Assert
        sut.ToExpression().Compile()(4).ShouldBe(sut.Matches(4));
        sut.ToExpression().Compile()(1).ShouldBe(sut.Matches(1));
    }

    [Fact]
    public void Should_compile_and_agree_with_matches_for_or_with_policy_first()
    {
        // Act — static typing is the assertion: this must compile and remain an ExpressionSpecBase
        ExpressionSpecBase<int, string> sut = PolicyOperand | SpecOperand;

        // Assert
        sut.ToExpression().Compile()(4).ShouldBe(sut.Matches(4));
        sut.ToExpression().Compile()(1).ShouldBe(sut.Matches(1));
    }

    [Fact]
    public void Should_compile_and_agree_with_matches_for_xor_with_spec_first()
    {
        // Act — static typing is the assertion: this must compile and remain an ExpressionSpecBase
        ExpressionSpecBase<int, string> sut = SpecOperand ^ PolicyOperand;

        // Assert
        sut.ToExpression().Compile()(2).ShouldBe(sut.Matches(2));
        sut.ToExpression().Compile()(4).ShouldBe(sut.Matches(4));
    }

    [Fact]
    public void Should_compile_and_agree_with_matches_for_xor_with_policy_first()
    {
        // Act — static typing is the assertion: this must compile and remain an ExpressionSpecBase
        ExpressionSpecBase<int, string> sut = PolicyOperand ^ SpecOperand;

        // Assert
        sut.ToExpression().Compile()(2).ShouldBe(sut.Matches(2));
        sut.ToExpression().Compile()(4).ShouldBe(sut.Matches(4));
    }

    [Fact]
    public void Should_bind_the_hidden_sibling_redeclare_when_anding_an_untyped_ordinary_spec()
    {
        // Arrange — a non-string-metadata expression policy, and an ordinary spec with a third, different
        // metadata type, so the call can only bind to the SpecBase<TModel> (untyped-metadata) redeclare.
        var guidPolicy = new ExpressionPolicyDecorator<int, Guid>(
            Spec.Build((int n) => n > 3).WhenTrue(Guid.Empty).WhenFalse(Guid.NewGuid()).Create("is big"),
            n => n > 3);
        var boolSpec = Spec.Build((int n) => n % 2 == 0).WhenTrue(true).WhenFalse(false).Create("is even");

        // Act — static typing is the assertion: without the redeclare, this call is CS0311
        SpecBase<int, string> act = guidPolicy.And(boolSpec);

        // Assert
        act.ShouldNotBeNull();
    }
}
