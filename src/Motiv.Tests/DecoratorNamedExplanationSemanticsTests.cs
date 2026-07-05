namespace Motiv.Tests;

public class DecoratorNamedExplanationSemanticsTests
{
    private static SpecBase<bool, string> UnderlyingSpec => Spec.Build((bool b) => b).Create("underlying");
    private static PolicyBase<bool, string> UnderlyingPolicy => Spec.Build((bool b) => b).Create("underlying");

    [Theory]
    [InlineAutoData(true, "is accepted == true")]
    [InlineAutoData(false, "is accepted == false")]
    public void Spec_named_explanation_asserts_statement_suffix_and_retains_values(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build(UnderlyingSpec)
            .WhenTrue("yes")
            .WhenFalse("no")
            .Create("is accepted");

        // Act
        var result = spec.Evaluate(model);

        // Assert
        result.Assertions.ShouldBe([expected]);
        result.Reason.ShouldBe(expected);
        result.Values.ShouldBe([model ? "yes" : "no"]);
    }

    [Theory]
    [InlineAutoData(true, "is accepted == true")]
    [InlineAutoData(false, "is accepted == false")]
    public void Policy_named_explanation_asserts_statement_suffix_and_retains_values(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build(UnderlyingPolicy)
            .WhenTrue("yes")
            .WhenFalse("no")
            .Create("is accepted");

        // Act
        var result = spec.Evaluate(model);

        // Assert
        result.Assertions.ShouldBe([expected]);
        result.Reason.ShouldBe(expected);
        result.Values.ShouldBe([model ? "yes" : "no"]);
    }

    [Theory]
    [InlineAutoData(true, "yes")]
    [InlineAutoData(false, "no")]
    public void Spec_unnamed_explanation_keeps_strings_as_assertions(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build(UnderlyingSpec)
            .WhenTrue("yes")
            .WhenFalse("no")
            .Create();

        // Act
        var result = spec.Evaluate(model);

        // Assert
        result.Assertions.ShouldBe([expected]);
    }

    [Theory]
    [InlineAutoData(true, "yes")]
    [InlineAutoData(false, "no")]
    public void Policy_unnamed_explanation_keeps_strings_as_assertions(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build(UnderlyingPolicy)
            .WhenTrue("yes")
            .WhenFalse("no")
            .Create();

        // Act
        var result = spec.Evaluate(model);

        // Assert
        result.Assertions.ShouldBe([expected]);
    }

    [Fact]
    public void Spec_unnamed_explanation_falls_back_for_degenerate_false_because()
    {
        // Arrange
        var spec = Spec
            .Build(UnderlyingSpec)
            .WhenTrue("yes")
            .WhenFalse((_, _) => "   ")
            .Create();

        // Act
        var result = spec.Evaluate(false);

        // Assert
        result.Assertions.ShouldBe(["yes == false"]);
        result.Values.ShouldBe(["   "]);
    }

    [Fact]
    public void Policy_unnamed_explanation_falls_back_for_degenerate_false_because()
    {
        // Arrange
        var spec = Spec
            .Build(UnderlyingPolicy)
            .WhenTrue("yes")
            .WhenFalse((_, _) => "   ")
            .Create();

        // Act
        var result = spec.Evaluate(false);

        // Assert
        result.Assertions.ShouldBe(["yes == false"]);
        result.Values.ShouldBe(["   "]);
    }

    [Fact]
    public void Spec_unnamed_create_throws_on_whitespace_true_because()
    {
        // Act
        var act = () => Spec
            .Build(UnderlyingSpec)
            .WhenTrue(" ")
            .WhenFalse("no")
            .Create();

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Policy_unnamed_create_throws_on_whitespace_true_because()
    {
        // Act
        var act = () => Spec
            .Build(UnderlyingPolicy)
            .WhenTrue(" ")
            .WhenFalse("no")
            .Create();

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Theory]
    [InlineAutoData(true, "is accepted == true")]
    [InlineAutoData(false, "is accepted == false")]
    public void Spec_named_multi_assertion_asserts_statement_suffix(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build(UnderlyingSpec)
            .WhenTrue("yes")
            .WhenFalseYield((_, _) => ["no", "not at all"])
            .Create("is accepted");

        // Act
        var result = spec.Evaluate(model);

        // Assert
        result.Assertions.ShouldBe([expected]);
    }

    [Theory]
    [InlineAutoData(true, "is accepted == true")]
    [InlineAutoData(false, "is accepted == false")]
    public void Policy_named_multi_assertion_asserts_statement_suffix(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build(UnderlyingPolicy)
            .WhenTrue("yes")
            .WhenFalseYield((_, _) => ["no", "not at all"])
            .Create("is accepted");

        // Act
        var result = spec.Evaluate(model);

        // Assert
        result.Assertions.ShouldBe([expected]);
    }

    [Fact]
    public void Spec_unnamed_multi_assertion_keeps_yielded_strings()
    {
        // Arrange
        var spec = Spec
            .Build(UnderlyingSpec)
            .WhenTrue("yes")
            .WhenFalseYield((_, _) => ["no", "not at all"])
            .Create();

        // Act
        var result = spec.Evaluate(false);

        // Assert
        result.Assertions.ShouldBe(["no", "not at all"]);
    }

    [Fact]
    public void Policy_unnamed_multi_assertion_keeps_yielded_strings()
    {
        // Arrange
        var spec = Spec
            .Build(UnderlyingPolicy)
            .WhenTrue("yes")
            .WhenFalseYield((_, _) => ["no", "not at all"])
            .Create();

        // Act
        var result = spec.Evaluate(false);

        // Assert
        result.Assertions.ShouldBe(["no", "not at all"]);
    }
}
