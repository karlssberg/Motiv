namespace Motiv.Tests;

public class BooleanPredicateNamedExplanationSemanticsTests
{
    [Theory]
    [InlineAutoData(true, "is even == true")]
    [InlineAutoData(false, "is even == false")]
    public void Named_explanation_spec_asserts_statement_suffix(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => b)
            .WhenTrue("yes")
            .WhenFalse("no")
            .Create("is even");

        // Act
        var result = spec.Evaluate(model);

        // Assert
        result.Assertions.ShouldBe([expected]);
        result.Reason.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(true, "yes")]
    [InlineAutoData(false, "no")]
    public void Named_explanation_spec_keeps_strings_as_metadata(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => b)
            .WhenTrue("yes")
            .WhenFalse("no")
            .Create("is even");

        // Act
        var result = spec.Evaluate(model);

        // Assert
        result.Values.ShouldBe([expected]);
    }

    [Theory]
    [InlineAutoData(true, "yes")]
    [InlineAutoData(false, "no")]
    public void Unnamed_explanation_spec_keeps_strings_as_assertions(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => b)
            .WhenTrue("yes")
            .WhenFalse("no")
            .Create();

        // Act
        var result = spec.Evaluate(model);

        // Assert
        result.Assertions.ShouldBe([expected]);
        result.Reason.ShouldBe(expected);
    }

    [Fact]
    public void Unnamed_explanation_spec_falls_back_to_reason_for_degenerate_delegate_output()
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => b)
            .WhenTrue("yes")
            .WhenFalse(_ => "   ")
            .Create();

        // Act
        var result = spec.Evaluate(false);

        // Assert
        result.Assertions.ShouldBe(["yes == false"]);
    }

    [Fact]
    public void Unnamed_create_throws_on_whitespace_true_because()
    {
        // Act
        var act = () => Spec
            .Build((bool b) => b)
            .WhenTrue("   ")
            .WhenFalse("no")
            .Create();

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Named_create_accepts_whitespace_because_strings()
    {
        // Act
        var act = () => Spec
            .Build((bool b) => b)
            .WhenTrue("   ")
            .WhenFalse("")
            .Create("is even");

        // Assert
        act.ShouldNotThrow();
    }

    [Theory]
    [InlineAutoData(true, "is even == true")]
    [InlineAutoData(false, "is even == false")]
    public void Named_multi_assertion_spec_asserts_statement_suffix(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => b)
            .WhenTrue("yes")
            .WhenFalseYield(_ => ["no", "definitely not"])
            .Create("is even");

        // Act
        var result = spec.Evaluate(model);

        // Assert
        result.Assertions.ShouldBe([expected]);
    }

    [Fact]
    public void Unnamed_multi_assertion_spec_keeps_yielded_strings_as_assertions()
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => b)
            .WhenTrue("yes")
            .WhenFalseYield(_ => ["no", "definitely not"])
            .Create();

        // Act
        var result = spec.Evaluate(false);

        // Assert
        result.Assertions.ShouldBe(["no", "definitely not"]);
    }

    [Theory]
    [InlineAutoData(true, "is even == true")]
    [InlineAutoData(false, "is even == false")]
    public void Named_delegate_explanation_spec_asserts_statement_suffix(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => b)
            .WhenTrue(_ => "yes")
            .WhenFalse(_ => "no")
            .Create("is even");

        // Act
        var result = spec.Evaluate(model);

        // Assert
        result.Assertions.ShouldBe([expected]);
    }
}
