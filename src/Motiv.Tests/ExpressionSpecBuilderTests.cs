using System.Linq.Expressions;

namespace Motiv.Tests;

public class ExpressionSpecBuilderTests
{
    [Fact]
    public void Should_create_an_expression_spec_from_a_minimal_bool_expression_proposition()
    {
        // Act — static typing is the assertion: this must compile against ExpressionSpecBase
        ExpressionSpecBase<int, string> act = Spec.From((int n) => n > 3).Create("is greater than three");

        // Assert
        act.ToExpression().Compile()(4).ShouldBeTrue();
        act.Evaluate(4).Reason.ShouldBe("is greater than three == true");
    }

    [Fact]
    public void Should_return_the_original_lambda_from_a_from_built_proposition()
    {
        // Arrange
        Expression<Func<int, bool>> expression = n => n > 3;

        // Act
        var act = Spec.From(expression).Create("is greater than three");

        // Assert
        act.ToExpression().ShouldBeSameAs(expression);
    }

    [Fact]
    public void Should_create_an_expression_policy_from_an_explanation_proposition()
    {
        // Act
        ExpressionPolicyBase<int, string> act = Spec
            .From((int n) => n > 3)
            .WhenTrue("is greater than three")
            .WhenFalse("is not greater than three")
            .Create();

        // Assert
        act.Evaluate(2).Value.ShouldBe("is not greater than three");
        act.ToExpression().Compile()(2).ShouldBeFalse();
    }

    [Fact]
    public void Should_create_an_expression_policy_from_a_metadata_proposition()
    {
        // Act
        ExpressionPolicyBase<int, Guid> act = Spec
            .From((int n) => n > 3)
            .WhenTrue(Guid.Empty)
            .WhenFalse(Guid.NewGuid())
            .Create("is greater than three");

        // Assert
        act.Evaluate(4).Value.ShouldBe(Guid.Empty);
        act.ToExpression().Compile()(4).ShouldBeTrue();
    }

    [Fact]
    public void Should_create_an_expression_spec_from_a_multi_assertion_proposition()
    {
        // Act
        ExpressionSpecBase<int, string> act = Spec
            .From((int n) => n > 3)
            .WhenTrueYield((_, _) => ["big"])
            .WhenFalseYield((_, _) => ["small"])
            .Create("is greater than three");

        // Assert - named multi-assertion explanation specs surface the underlying decomposed clause
        // assertions (ExpressionTreeMetadataProposition-style named rule), not the because-strings.
        act.Evaluate(4).Assertions.ShouldBe(["n > 3"]);
    }

    [Fact]
    public void Should_keep_boolean_result_lambdas_on_the_ordinary_hierarchy()
    {
        // Arrange
        var inner = Spec.Build((int n) => n > 3).Create("inner");

        // Act — lambda returns BooleanResultBase<string>, not bool
        var act = Spec.From((int n) => inner.Evaluate(n)).Create("wraps a result");

        // Assert
        act.ShouldBeAssignableTo<SpecBase<int, string>>();
        (act is IExpressionSpec<int>).ShouldBeFalse();
    }

    [Fact]
    public void Should_keep_higher_order_from_chains_compiling_for_bool_lambdas()
    {
        // Act
        var act = Spec
            .From((int n) => n > 3)
            .AsAnySatisfied()
            .Create("any are greater than three");

        // Assert
        act.Evaluate([1, 2, 4]).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_compose_two_from_built_propositions_and_recover_the_combined_expression()
    {
        // Arrange
        var adult = Spec.From((int age) => age >= 18).Create("is adult");
        var senior = Spec.From((int age) => age >= 65).Create("is senior");

        // Act
        var act = adult.And(!senior);

        // Assert
        act.ShouldBeAssignableTo<IExpressionSpec<int>>();
        act.ToExpression().Compile()(30).ShouldBeTrue();
        act.ToExpression().Compile()(70).ShouldBeFalse();
    }

    [Fact]
    public void Should_create_an_expression_spec_from_a_singular_when_true_with_when_false_yield_proposition()
    {
        // Act — a WhenTrue lambda (rather than a scalar) is required to bind to the explanation-specific
        // overload set (WhenTrueLambdaOverloads) instead of the generic metadata one (WhenTrueOverloads),
        // which also accepts bare scalars.
        ExpressionSpecBase<int, string> act = Spec
            .From((int n) => n > 3)
            .WhenTrue(_ => "big")
            .WhenFalseYield((_, _) => ["small", "not enough"])
            .Create("is greater than three");

        // Assert - named rule: Assertions surface the underlying decomposed clause assertions.
        act.Evaluate(4).Assertions.ShouldBe(["n > 3"]);
        act.Evaluate(2).Assertions.ShouldBe(["n <= 3"]);
        act.ToExpression().Compile()(4).ShouldBeTrue();
    }

    [Fact]
    public void Should_create_an_unnamed_higher_order_expression_backed_metadata_proposition()
    {
        // Act — no explicit statement, so the name is derived from the expression tree itself
        var act = Spec
            .From((int n) => n % 2 == 0)
            .AsAllSatisfied()
            .WhenTrue(Guid.Empty)
            .WhenFalse(Guid.NewGuid())
            .Create();

        // Assert
        act.Evaluate([2, 4, 6]).Value.ShouldBe(Guid.Empty);
        act.Evaluate([2, 4, 6]).Satisfied.ShouldBeTrue();
        act.Evaluate([2, 3, 6]).Satisfied.ShouldBeFalse();
    }
}
