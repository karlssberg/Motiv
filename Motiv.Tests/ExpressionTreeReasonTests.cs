namespace Motiv.Tests;

public class ExpressionTreeReasonests
{
    [Fact]
    public void Should_yield_true_reason()
    {
        // Assemble
        var literal = Spec
            .From((int n) => n > 0)
            .Create("is-positive");

        var modelCallback = Spec
            .From((int n) => n > 0)
            .Create("is-positive");

        var resultCallback = Spec
            .From((int n) => n > 0)
            .Create("is-positive");

        var multipleCallback = Spec
            .From((int n) => n > 0)
            .Create("is-positive");

        var spec = literal | modelCallback | resultCallback | multipleCallback;

        // Act
        var act = spec.IsSatisfiedBy(1);

        // Assert
        act.Reason.Should().BeEquivalentTo("is-positive | is-positive | is-positive | is-positive");
    }

    [Fact]
    public void Should_yield_true_reason_when_overriding_assertion()
    {
        // Assemble
        var literal = Spec
            .From((int n) => n > 0)
            .WhenTrue("is positive")
            .WhenFalse("invalid")
            .Create("invalid");

        var modelCallback = Spec
            .From((int n) => n > 0)
            .WhenTrue(_ => "is positive")
            .WhenFalse("invalid")
            .Create("invalid");

        var resultCallback = Spec
            .From((int n) => n > 0)
            .WhenTrue((_, _) => "is positive")
            .WhenFalse("invalid")
            .Create("invalid");

        var multipleCallback = Spec
            .From((int n) => n > 0)
            .WhenTrueYield((_, _) => ["invalid"])
            .WhenFalse("invalid")
            .Create("is positive");

        var spec = literal | modelCallback | resultCallback | multipleCallback;

        // Act
        var act = spec.IsSatisfiedBy(1);

        // Assert
        act.Reason.Should().BeEquivalentTo("is positive | is positive | is positive | is positive");
    }
}
