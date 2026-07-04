using Motiv.ExpressionTreeProposition;

namespace Motiv.Tests;

public class ExpressionPolicyCompositionTests
{
    private static ExpressionPolicyBase<int, string> IsPositivePolicy() =>
        Spec.From((int n) => n > 0).WhenTrue("is positive").WhenFalse("is not positive").Create();

    private static ExpressionSpecBase<int, string> IsSmallSpec() =>
        Spec.From((int n) => n < 100).Create("is small");

    private static ExpressionPolicyBase<int, string> IsSmallPolicy() =>
        Spec.From((int n) => n < 100).WhenTrue("is small").WhenFalse("is not small").Create();

    private static SpecBase<int, string> IsSmallOrdinarySpec() =>
        Spec.Build((int n) => n < 100).WhenTrue("is small").WhenFalse("is not small").Create();

    private static SpecBase<int> IsSmallDifferentMetadataOrdinarySpec() =>
        Spec.Build((int n) => n < 100)
            .WhenTrue(Guid.NewGuid())
            .WhenFalse(Guid.NewGuid())
            .Create("is small");

    private static ExpressionPolicyBase<int, int> IsSmallDifferentMetadataExpressionPolicy() =>
        new ExpressionPolicyDecorator<int, int>(
            Spec.Build((int n) => n < 100).WhenTrue(0).WhenFalse(422).Create("is small"),
            n => n < 100);

    [Theory]
    [InlineData(4, true)]
    [InlineData(-3, false)]
    public void Should_and_a_policy_with_a_same_metadata_expression_spec(int model, bool expected)
    {
        var sut = IsPositivePolicy().And(IsSmallSpec());

        sut.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(-3, false)]
    public void Should_and_a_policy_with_a_same_metadata_expression_policy(int model, bool expected)
    {
        var sut = IsPositivePolicy().And(IsSmallPolicy());

        sut.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(-3, false)]
    public void Should_degrade_to_an_ordinary_spec_when_anding_a_policy_with_a_same_metadata_non_expression_spec(int model, bool expected)
    {
        var sut = IsPositivePolicy().And(IsSmallOrdinarySpec());

        (sut is IExpressionSpec<int>).ShouldBeFalse();
        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(-3, false)]
    public void Should_and_a_policy_with_a_different_metadata_expression_spec_via_the_generic_overload(int model, bool expected)
    {
        var sut = IsPositivePolicy().And(IsSmallDifferentMetadataExpressionPolicy());

        sut.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(-3, false)]
    public void Should_and_also_a_policy_with_a_same_metadata_expression_spec(int model, bool expected)
    {
        var sut = IsPositivePolicy().AndAlso(IsSmallSpec());

        sut.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(-3, false)]
    public void Should_and_also_a_policy_with_a_same_metadata_expression_policy(int model, bool expected)
    {
        var sut = IsPositivePolicy().AndAlso(IsSmallPolicy());

        sut.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(-3, false)]
    public void Should_degrade_to_an_ordinary_spec_when_and_alsoing_a_policy_with_a_same_metadata_non_expression_spec(int model, bool expected)
    {
        var sut = IsPositivePolicy().AndAlso(IsSmallOrdinarySpec());

        (sut is IExpressionSpec<int>).ShouldBeFalse();
        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(-3, false)]
    public void Should_compose_and_also_with_a_policy_and_a_different_metadata_non_expression_spec_via_the_untyped_overload(int model, bool expected)
    {
        var sut = IsPositivePolicy().AndAlso(IsSmallDifferentMetadataOrdinarySpec());

        sut.ShouldBeAssignableTo<SpecBase<int, string>>();
        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(-3, false)]
    public void Should_and_also_a_policy_with_a_different_metadata_expression_spec_via_the_generic_overload(int model, bool expected)
    {
        var sut = IsPositivePolicy().AndAlso(IsSmallDifferentMetadataExpressionPolicy());

        sut.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(-3, true)]
    public void Should_or_a_policy_with_a_same_metadata_expression_spec(int model, bool expected)
    {
        var sut = IsPositivePolicy().Or(IsSmallSpec());

        sut.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(-3, true)]
    public void Should_or_a_policy_with_a_same_metadata_expression_policy(int model, bool expected)
    {
        var sut = IsPositivePolicy().Or(IsSmallPolicy());

        sut.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(-3, true)]
    public void Should_degrade_to_an_ordinary_spec_when_oring_a_policy_with_a_same_metadata_non_expression_spec(int model, bool expected)
    {
        var sut = IsPositivePolicy().Or(IsSmallOrdinarySpec());

        (sut is IExpressionSpec<int>).ShouldBeFalse();
        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(-3, true)]
    public void Should_compose_or_with_a_policy_and_a_different_metadata_non_expression_spec_via_the_untyped_overload(int model, bool expected)
    {
        var sut = IsPositivePolicy().Or(IsSmallDifferentMetadataOrdinarySpec());

        sut.ShouldBeAssignableTo<SpecBase<int, string>>();
        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(-3, true)]
    public void Should_or_a_policy_with_a_different_metadata_expression_spec_via_the_generic_overload(int model, bool expected)
    {
        var sut = IsPositivePolicy().Or(IsSmallDifferentMetadataExpressionPolicy());

        sut.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(-3, true)]
    public void Should_or_else_a_policy_with_a_same_metadata_expression_spec(int model, bool expected)
    {
        var sut = IsPositivePolicy().OrElse(IsSmallSpec());

        sut.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(-3, true)]
    public void Should_degrade_to_an_ordinary_spec_when_or_elseing_a_policy_with_a_same_metadata_non_expression_spec(int model, bool expected)
    {
        var sut = IsPositivePolicy().OrElse(IsSmallOrdinarySpec());

        (sut is IExpressionSpec<int>).ShouldBeFalse();
        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(-3, true)]
    public void Should_compose_or_else_with_a_policy_and_a_different_metadata_non_expression_spec_via_the_untyped_overload(int model, bool expected)
    {
        var sut = IsPositivePolicy().OrElse(IsSmallDifferentMetadataOrdinarySpec());

        sut.ShouldBeAssignableTo<SpecBase<int, string>>();
        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(-3, true)]
    public void Should_or_else_a_policy_with_a_different_metadata_expression_spec_via_the_generic_overload(int model, bool expected)
    {
        var sut = IsPositivePolicy().OrElse(IsSmallDifferentMetadataExpressionPolicy());

        sut.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(-3, true)]
    public void Should_xor_a_policy_with_a_same_metadata_expression_spec(int model, bool expected)
    {
        var sut = IsPositivePolicy().XOr(IsSmallSpec());

        sut.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(-3, true)]
    public void Should_xor_a_policy_with_a_same_metadata_expression_policy(int model, bool expected)
    {
        var sut = IsPositivePolicy().XOr(IsSmallPolicy());

        sut.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(-3, true)]
    public void Should_degrade_to_an_ordinary_spec_when_xoring_a_policy_with_a_same_metadata_non_expression_spec(int model, bool expected)
    {
        var sut = IsPositivePolicy().XOr(IsSmallOrdinarySpec());

        (sut is IExpressionSpec<int>).ShouldBeFalse();
        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(-3, true)]
    public void Should_compose_xor_with_a_policy_and_a_different_metadata_non_expression_spec_via_the_untyped_overload(int model, bool expected)
    {
        var sut = IsPositivePolicy().XOr(IsSmallDifferentMetadataOrdinarySpec());

        sut.ShouldBeAssignableTo<SpecBase<int, string>>();
        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(-3, true)]
    public void Should_xor_a_policy_with_a_different_metadata_expression_spec_via_the_generic_overload(int model, bool expected)
    {
        var sut = IsPositivePolicy().XOr(IsSmallDifferentMetadataExpressionPolicy());

        sut.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        sut.Matches(model).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(-3, true)]
    public void Should_xor_two_expression_policies_via_the_operator(int model, bool expected)
    {
        // Arrange — statically typed as ExpressionPolicyBase so overload resolution binds to the
        // operator declared on ExpressionPolicyBase rather than the ExpressionSpecBase one.
        ExpressionPolicyBase<int, string> left = IsPositivePolicy();
        ExpressionPolicyBase<int, string> right = IsSmallPolicy();

        var sut = left ^ right;

        sut.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        sut.Matches(model).ShouldBe(expected);
    }
}
