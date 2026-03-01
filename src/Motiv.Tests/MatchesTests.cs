namespace Motiv.Tests;

public class MatchesTests
{
    // --- Leaf: Minimal Proposition ---

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_return_correct_bool_for_minimal_proposition(
        bool expected,
        object model)
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => expected)
            .Create("is expected");

        // Act
        var act = spec.Matches(model);

        // Assert
        act.ShouldBe(expected);
    }

    // --- Leaf: Explanation Proposition ---

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_return_correct_bool_for_explanation_proposition(
        bool expected,
        object model)
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => expected)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .Create();

        // Act
        var act = spec.Matches(model);

        // Assert
        act.ShouldBe(expected);
    }

    // --- Leaf: Metadata Proposition ---

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_return_correct_bool_for_metadata_proposition(
        bool expected,
        object model)
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => expected)
            .WhenTrue(100)
            .WhenFalse(-1)
            .Create("has metadata");

        // Act
        var act = spec.Matches(model);

        // Assert
        act.ShouldBe(expected);
    }

    // --- AND operator ---

    [Theory]
    [InlineAutoData(true, true, true)]
    [InlineAutoData(true, false, false)]
    [InlineAutoData(false, true, false)]
    [InlineAutoData(false, false, false)]
    public void Should_return_correct_bool_for_and_operator(
        bool leftResult,
        bool rightResult,
        bool expected,
        object model)
    {
        // Arrange
        var left = Spec.Build<object>(_ => leftResult).Create("left");
        var right = Spec.Build<object>(_ => rightResult).Create("right");
        var spec = left & right;

        // Act
        var act = spec.Matches(model);

        // Assert
        act.ShouldBe(expected);
    }

    // --- OR operator ---

    [Theory]
    [InlineAutoData(true, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(false, false, false)]
    public void Should_return_correct_bool_for_or_operator(
        bool leftResult,
        bool rightResult,
        bool expected,
        object model)
    {
        // Arrange
        var left = Spec.Build<object>(_ => leftResult).Create("left");
        var right = Spec.Build<object>(_ => rightResult).Create("right");
        var spec = left | right;

        // Act
        var act = spec.Matches(model);

        // Assert
        act.ShouldBe(expected);
    }

    // --- XOR operator ---

    [Theory]
    [InlineAutoData(true, true, false)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(false, false, false)]
    public void Should_return_correct_bool_for_xor_operator(
        bool leftResult,
        bool rightResult,
        bool expected,
        object model)
    {
        // Arrange
        var left = Spec.Build<object>(_ => leftResult).Create("left");
        var right = Spec.Build<object>(_ => rightResult).Create("right");
        var spec = left ^ right;

        // Act
        var act = spec.Matches(model);

        // Assert
        act.ShouldBe(expected);
    }

    // --- AndAlso (short-circuit AND) ---

    [Theory]
    [InlineAutoData(true, true, true)]
    [InlineAutoData(true, false, false)]
    [InlineAutoData(false, true, false)]
    [InlineAutoData(false, false, false)]
    public void Should_return_correct_bool_for_and_also_operator(
        bool leftResult,
        bool rightResult,
        bool expected,
        object model)
    {
        // Arrange
        var left = Spec.Build<object>(_ => leftResult).Create("left");
        var right = Spec.Build<object>(_ => rightResult).Create("right");
        var spec = left.AndAlso(right);

        // Act
        var act = spec.Matches(model);

        // Assert
        act.ShouldBe(expected);
    }

    // --- OrElse (short-circuit OR) ---

    [Theory]
    [InlineAutoData(true, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(false, false, false)]
    public void Should_return_correct_bool_for_or_else_operator(
        bool leftResult,
        bool rightResult,
        bool expected,
        object model)
    {
        // Arrange
        var left = Spec.Build<object>(_ => leftResult).Create("left");
        var right = Spec.Build<object>(_ => rightResult).Create("right");
        var spec = left.OrElse(right);

        // Act
        var act = spec.Matches(model);

        // Assert
        act.ShouldBe(expected);
    }

    // --- NOT operator ---

    [Theory]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    public void Should_return_correct_bool_for_not_operator(
        bool input,
        bool expected,
        object model)
    {
        // Arrange
        var inner = Spec.Build<object>(_ => input).Create("inner");
        var spec = !inner;

        // Act
        var act = spec.Matches(model);

        // Assert
        act.ShouldBe(expected);
    }

    // --- Decorated spec ---

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_return_correct_bool_for_decorated_spec(
        bool expected,
        object model)
    {
        // Arrange
        var underlying = Spec
            .Build<object>(_ => expected)
            .Create("underlying");

        var spec = Spec
            .Build(underlying)
            .WhenTrue("decorated true")
            .WhenFalse("decorated false")
            .Create("decorated");

        // Act
        var act = spec.Matches(model);

        // Assert
        act.ShouldBe(expected);
    }

    // --- ChangeModelTo spec ---

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_return_correct_bool_for_change_model_spec(
        bool expected)
    {
        // Arrange
        var inner = Spec
            .Build((int n) => expected)
            .Create("inner");

        var spec = inner.ChangeModelTo<string>(s => s.Length);

        // Act
        var act = spec.Matches("hello");

        // Assert
        act.ShouldBe(expected);
    }

    // --- Matches agrees with IsSatisfiedBy().Satisfied ---

    [Theory]
    [InlineAutoData(true, true)]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(false, false)]
    public void Should_agree_with_is_satisfied_by_for_compositions(
        bool leftResult,
        bool rightResult,
        object model)
    {
        // Arrange
        var left = Spec.Build<object>(_ => leftResult).Create("left");
        var right = Spec.Build<object>(_ => rightResult).Create("right");

        var andSpec = left & right;
        var orSpec = left | right;
        var xorSpec = left ^ right;
        var notSpec = !left;
        var andAlsoSpec = left.AndAlso(right);
        var orElseSpec = left.OrElse(right);

        // Act & Assert
        andSpec.Matches(model).ShouldBe(andSpec.IsSatisfiedBy(model).Satisfied);
        orSpec.Matches(model).ShouldBe(orSpec.IsSatisfiedBy(model).Satisfied);
        xorSpec.Matches(model).ShouldBe(xorSpec.IsSatisfiedBy(model).Satisfied);
        notSpec.Matches(model).ShouldBe(notSpec.IsSatisfiedBy(model).Satisfied);
        andAlsoSpec.Matches(model).ShouldBe(andAlsoSpec.IsSatisfiedBy(model).Satisfied);
        orElseSpec.Matches(model).ShouldBe(orElseSpec.IsSatisfiedBy(model).Satisfied);
    }

    // --- Implicit Func<TModel, bool> conversion uses Matches ---

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_use_matches_for_implicit_func_conversion(
        bool expected,
        object model)
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => expected)
            .Create("test spec");

        Func<object, bool> predicate = spec;

        // Act
        var act = predicate(model);

        // Assert
        act.ShouldBe(expected);
    }

    // --- OrElse on Policy ---

    [Theory]
    [InlineAutoData(true, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(false, false, false)]
    public void Should_return_correct_bool_for_or_else_policy(
        bool leftResult,
        bool rightResult,
        bool expected,
        object model)
    {
        // Arrange
        var left = Spec
            .Build<object>(_ => leftResult)
            .WhenTrue("left true")
            .WhenFalse("left false")
            .Create("left");

        var right = Spec
            .Build<object>(_ => rightResult)
            .WhenTrue("right true")
            .WhenFalse("right false")
            .Create("right");

        var spec = left.OrElse(right);

        // Act
        var act = spec.Matches(model);

        // Assert
        act.ShouldBe(expected);
    }

    // --- NOT on Policy ---

    [Theory]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    public void Should_return_correct_bool_for_not_policy(
        bool input,
        bool expected,
        object model)
    {
        // Arrange
        var inner = Spec
            .Build<object>(_ => input)
            .WhenTrue("is true")
            .WhenFalse("is false")
            .Create("inner");

        var spec = !inner;

        // Act
        var act = spec.Matches(model);

        // Assert
        act.ShouldBe(expected);
    }

    // --- Expression tree spec ---

    [Theory]
    [InlineAutoData(4, true)]
    [InlineAutoData(3, false)]
    public void Should_return_correct_bool_for_expression_tree_spec(
        int model,
        bool expected)
    {
        // Arrange
        var spec = Spec
            .From((int n) => n % 2 == 0)
            .Create("is even");

        // Act
        var act = spec.Matches(model);

        // Assert
        act.ShouldBe(expected);
    }

    // --- MultiValue proposition ---

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_return_correct_bool_for_multi_value_proposition(
        bool expected,
        object model)
    {
        // Arrange
        var spec = Spec
            .Build<object>(_ => expected)
            .WhenTrueYield(_ => new[] { "yes", "affirmative" })
            .WhenFalseYield(_ => new[] { "no", "negative" })
            .Create("multi");

        // Act
        var act = spec.Matches(model);

        // Assert
        act.ShouldBe(expected);
    }

    // --- Short-circuit: AndAlso should not evaluate right when left is false ---

    [Fact]
    public void Should_short_circuit_and_also_matches()
    {
        // Arrange
        var rightEvaluated = false;

        var left = Spec.Build<object>(_ => false).Create("left");
        var right = Spec.Build<object>(_ =>
        {
            rightEvaluated = true;
            return true;
        }).Create("right");

        var spec = left.AndAlso(right);

        // Act
        spec.Matches(new object());

        // Assert
        rightEvaluated.ShouldBeFalse();
    }

    // --- Short-circuit: OrElse should not evaluate right when left is true ---

    [Fact]
    public void Should_short_circuit_or_else_matches()
    {
        // Arrange
        var rightEvaluated = false;

        var left = Spec.Build<object>(_ => true).Create("left");
        var right = Spec.Build<object>(_ =>
        {
            rightEvaluated = true;
            return false;
        }).Create("right");

        var spec = left.OrElse(right);

        // Act
        spec.Matches(new object());

        // Assert
        rightEvaluated.ShouldBeFalse();
    }
}
