namespace Motiv.Tests.HigherOrderProposition;

public class HigherOrderNamedExplanationSemanticsTests
{
    [Theory]
    [InlineAutoData(new[] { 2, 4 }, "all even == true")]
    [InlineAutoData(new[] { 1, 4 }, "all even == false")]
    public void Named_higher_order_explanation_spec_asserts_statement_suffix(int[] models, string expected)
    {
        // Arrange
        var spec = Spec
            .Build((int n) => n % 2 == 0)
            .AsAllSatisfied()
            .WhenTrue("all are even")
            .WhenFalse("some are odd")
            .Create("all even");

        // Act
        var result = spec.Evaluate(models);

        // Assert
        result.Assertions.ShouldBe([expected]);
        result.Reason.ShouldBe(expected);
        result.Values.ShouldBe([models.All(n => n % 2 == 0) ? "all are even" : "some are odd"]);
    }

    [Theory]
    [InlineAutoData(new[] { 2, 4 }, "all are even")]
    [InlineAutoData(new[] { 1, 4 }, "some are odd")]
    public void Unnamed_higher_order_explanation_spec_keeps_strings(int[] models, string expected)
    {
        // Arrange
        var spec = Spec
            .Build((int n) => n % 2 == 0)
            .AsAllSatisfied()
            .WhenTrue("all are even")
            .WhenFalse("some are odd")
            .Create();

        // Act
        var result = spec.Evaluate(models);

        // Assert
        result.Assertions.ShouldBe([expected]);
    }

    [Fact]
    public void Unnamed_higher_order_create_throws_on_whitespace_true_because()
    {
        // Act
        var act = () => Spec
            .Build((int n) => n % 2 == 0)
            .AsAllSatisfied()
            .WhenTrue("  ")
            .WhenFalse("some are odd")
            .Create();

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Unnamed_higher_order_falls_back_for_degenerate_delegate_output()
    {
        // Arrange
        var spec = Spec
            .Build((int n) => n % 2 == 0)
            .AsAllSatisfied()
            .WhenTrue("all are even")
            .WhenFalse(_ => "")
            .Create();

        // Act
        var result = spec.Evaluate([1]);

        // Assert
        result.Assertions.ShouldBe(["all are even == false"]);
    }

    [Theory]
    [InlineAutoData(new[] { 2, 4 }, "all even == true")]
    [InlineAutoData(new[] { 1, 4 }, "all even == false")]
    public void Named_higher_order_multi_assertion_spec_asserts_statement_suffix(int[] models, string expected)
    {
        // Arrange
        var spec = Spec
            .Build((int n) => n % 2 == 0)
            .AsAllSatisfied()
            .WhenTrue("all are even")
            .WhenFalseYield(_ => ["some are odd", "definitely not all even"])
            .Create("all even");

        // Act
        var result = spec.Evaluate(models);

        // Assert
        result.Assertions.ShouldBe([expected]);
    }

    [Fact]
    public void Unnamed_higher_order_multi_assertion_spec_keeps_yielded_strings()
    {
        // Arrange
        var spec = Spec
            .Build((int n) => n % 2 == 0)
            .AsAllSatisfied()
            .WhenTrue("all are even")
            .WhenFalseYield(_ => ["some are odd", "definitely not all even"])
            .Create();

        // Act
        var result = spec.Evaluate([1, 4]);

        // Assert
        result.Assertions.ShouldBe(["some are odd", "definitely not all even"]);
    }

    private static SpecBase<int, string> UnderlyingSpec =>
        Spec.Build((int n) => n % 2 == 0).Create("is even");

    [Theory]
    [InlineAutoData(new[] { 2, 4 }, "all even == true")]
    [InlineAutoData(new[] { 1, 4 }, "all even == false")]
    public void Named_higher_order_from_boolean_result_explanation_spec_asserts_statement_suffix(int[] models, string expected)
    {
        // Arrange
        var spec = Spec
            .Build(UnderlyingSpec)
            .AsAllSatisfied()
            .WhenTrue("all are even")
            .WhenFalse("some are odd")
            .Create("all even");

        // Act
        var result = spec.Evaluate(models);

        // Assert
        result.Assertions.ShouldBe([expected]);
        result.Reason.ShouldBe(expected);
        result.Values.ShouldBe([models.All(n => n % 2 == 0) ? "all are even" : "some are odd"]);
    }

    [Theory]
    [InlineAutoData(new[] { 2, 4 }, "all are even")]
    [InlineAutoData(new[] { 1, 4 }, "some are odd")]
    public void Unnamed_higher_order_from_boolean_result_explanation_spec_keeps_strings(int[] models, string expected)
    {
        // Arrange
        var spec = Spec
            .Build(UnderlyingSpec)
            .AsAllSatisfied()
            .WhenTrue("all are even")
            .WhenFalse("some are odd")
            .Create();

        // Act
        var result = spec.Evaluate(models);

        // Assert
        result.Assertions.ShouldBe([expected]);
    }

    [Fact]
    public void Unnamed_higher_order_from_boolean_result_create_throws_on_whitespace_true_because()
    {
        // Act
        var act = () => Spec
            .Build(UnderlyingSpec)
            .AsAllSatisfied()
            .WhenTrue("  ")
            .WhenFalse("some are odd")
            .Create();

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Unnamed_higher_order_from_boolean_result_falls_back_for_degenerate_delegate_output()
    {
        // Arrange
        var spec = Spec
            .Build(UnderlyingSpec)
            .AsAllSatisfied()
            .WhenTrue("all are even")
            .WhenFalse(_ => "")
            .Create();

        // Act
        var result = spec.Evaluate([1]);

        // Assert
        result.Assertions.ShouldBe(["all are even == false"]);
    }

    [Theory]
    [InlineAutoData(new[] { 2, 4 }, "all even == true")]
    [InlineAutoData(new[] { 1, 4 }, "all even == false")]
    public void Named_higher_order_from_boolean_result_multi_assertion_spec_asserts_statement_suffix(int[] models, string expected)
    {
        // Arrange
        var spec = Spec
            .Build(UnderlyingSpec)
            .AsAllSatisfied()
            .WhenTrue("all are even")
            .WhenFalseYield(_ => ["some are odd", "definitely not all even"])
            .Create("all even");

        // Act
        var result = spec.Evaluate(models);

        // Assert
        result.Assertions.ShouldBe([expected]);
        result.Values.ShouldBe(models.All(n => n % 2 == 0)
            ? ["all are even"]
            : ["some are odd", "definitely not all even"]);
    }

    [Fact]
    public void Unnamed_higher_order_from_boolean_result_multi_assertion_spec_keeps_yielded_strings()
    {
        // Arrange
        var spec = Spec
            .Build(UnderlyingSpec)
            .AsAllSatisfied()
            .WhenTrue("all are even")
            .WhenFalseYield(_ => ["some are odd", "definitely not all even"])
            .Create();

        // Act
        var result = spec.Evaluate([1, 4]);

        // Assert
        result.Assertions.ShouldBe(["some are odd", "definitely not all even"]);
    }

    private static PolicyBase<int, string> UnderlyingPolicy =>
        Spec.Build((int n) => n % 2 == 0).Create("is even");

    [Theory]
    [InlineAutoData(new[] { 2, 4 }, "all even == true")]
    [InlineAutoData(new[] { 1, 4 }, "all even == false")]
    public void Named_higher_order_from_policy_explanation_spec_asserts_statement_suffix(int[] models, string expected)
    {
        // Arrange
        var spec = Spec
            .Build(UnderlyingPolicy)
            .AsAllSatisfied()
            .WhenTrue("all are even")
            .WhenFalse("some are odd")
            .Create("all even");

        // Act
        var result = spec.Evaluate(models);

        // Assert
        result.Assertions.ShouldBe([expected]);
        result.Reason.ShouldBe(expected);
        result.Values.ShouldBe([models.All(n => n % 2 == 0) ? "all are even" : "some are odd"]);
    }

    [Theory]
    [InlineAutoData(new[] { 2, 4 }, "all are even")]
    [InlineAutoData(new[] { 1, 4 }, "some are odd")]
    public void Unnamed_higher_order_from_policy_explanation_spec_keeps_strings(int[] models, string expected)
    {
        // Arrange
        var spec = Spec
            .Build(UnderlyingPolicy)
            .AsAllSatisfied()
            .WhenTrue("all are even")
            .WhenFalse("some are odd")
            .Create();

        // Act
        var result = spec.Evaluate(models);

        // Assert
        result.Assertions.ShouldBe([expected]);
    }

    [Fact]
    public void Unnamed_higher_order_from_policy_create_throws_on_whitespace_true_because()
    {
        // Act
        var act = () => Spec
            .Build(UnderlyingPolicy)
            .AsAllSatisfied()
            .WhenTrue("  ")
            .WhenFalse("some are odd")
            .Create();

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Unnamed_higher_order_from_policy_falls_back_for_degenerate_delegate_output()
    {
        // Arrange
        var spec = Spec
            .Build(UnderlyingPolicy)
            .AsAllSatisfied()
            .WhenTrue("all are even")
            .WhenFalse(_ => "")
            .Create();

        // Act
        var result = spec.Evaluate([1]);

        // Assert
        result.Assertions.ShouldBe(["all are even == false"]);
    }

    [Theory]
    [InlineAutoData(new[] { 2, 4 }, "all even == true")]
    [InlineAutoData(new[] { 1, 4 }, "all even == false")]
    public void Named_higher_order_from_policy_multi_assertion_spec_asserts_statement_suffix(int[] models, string expected)
    {
        // Arrange
        var spec = Spec
            .Build(UnderlyingPolicy)
            .AsAllSatisfied()
            .WhenTrue("all are even")
            .WhenFalseYield(_ => ["some are odd", "definitely not all even"])
            .Create("all even");

        // Act
        var result = spec.Evaluate(models);

        // Assert
        result.Assertions.ShouldBe([expected]);
        result.Values.ShouldBe(models.All(n => n % 2 == 0)
            ? ["all are even"]
            : ["some are odd", "definitely not all even"]);
    }

    [Fact]
    public void Unnamed_higher_order_from_policy_multi_assertion_spec_keeps_yielded_strings()
    {
        // Arrange
        var spec = Spec
            .Build(UnderlyingPolicy)
            .AsAllSatisfied()
            .WhenTrue("all are even")
            .WhenFalseYield(_ => ["some are odd", "definitely not all even"])
            .Create();

        // Act
        var result = spec.Evaluate([1, 4]);

        // Assert
        result.Assertions.ShouldBe(["some are odd", "definitely not all even"]);
    }
}
