namespace Motiv.Tests;

public class BooleanResultPredicateNamedExplanationSemanticsTests
{
    private static SpecBase<bool, string> Underlying => Spec
        .Build((bool b) => b)
        .Create("underlying");

    [Theory]
    [InlineAutoData(true, "is accepted == true")]
    [InlineAutoData(false, "is accepted == false")]
    public void Named_explanation_spec_asserts_statement_suffix(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => Underlying.Evaluate(b))
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
    public void Unnamed_explanation_spec_keeps_strings_as_assertions(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => Underlying.Evaluate(b))
            .WhenTrue("yes")
            .WhenFalse("no")
            .Create();

        // Act
        var result = spec.Evaluate(model);

        // Assert
        result.Assertions.ShouldBe([expected]);
    }

    [Fact]
    public void Unnamed_explanation_spec_falls_back_for_degenerate_false_because()
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => Underlying.Evaluate(b))
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
    public void Unnamed_create_throws_on_whitespace_true_because()
    {
        // Act
        var act = () => Spec
            .Build((bool b) => Underlying.Evaluate(b))
            .WhenTrue(" ")
            .WhenFalse("no")
            .Create();

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Theory]
    [InlineAutoData(true, "is accepted == true")]
    [InlineAutoData(false, "is accepted == false")]
    public void Named_multi_assertion_spec_asserts_statement_suffix(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => Underlying.Evaluate(b))
            .WhenTrue("yes")
            .WhenFalseYield((_, _) => ["no", "not at all"])
            .Create("is accepted");

        // Act
        var result = spec.Evaluate(model);

        // Assert
        result.Assertions.ShouldBe([expected]);
    }

    [Fact]
    public void Unnamed_multi_assertion_spec_keeps_yielded_strings()
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => Underlying.Evaluate(b))
            .WhenTrue("yes")
            .WhenFalseYield((_, _) => ["no", "not at all"])
            .Create();

        // Act
        var result = spec.Evaluate(false);

        // Assert
        result.Assertions.ShouldBe(["no", "not at all"]);
    }

    [Theory]
    [InlineAutoData(true, "underlying == true")]
    [InlineAutoData(false, "underlying == false")]
    public void Minimal_spec_still_passes_underlying_assertions_through(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => Underlying.Evaluate(b))
            .Create("is accepted");

        // Act
        var result = spec.Evaluate(model);

        // Assert
        result.Assertions.ShouldBe([expected]);
    }
}
