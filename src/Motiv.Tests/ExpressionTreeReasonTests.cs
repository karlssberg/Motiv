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
        act.Reason.ShouldBe("(is-positive == true) | (is-positive == true) | (is-positive == true) | (is-positive == true)");
    }


    [Fact]
    public void Should_yield_true_reason_for_higher_order_propositions()
    {
        // Assemble
        var literal = Spec
            .From((int n) => n > 0)
            .AsAnySatisfied()
            .Create("is-positive");

        var modelCallback = Spec
            .From((int n) => n > 0)
            .AsAllSatisfied()
            .Create("is-positive");

        var resultCallback = Spec
            .From((int n) => n > 0)
            .AsNSatisfied(1)
            .Create("is-positive");

        var multipleCallback = Spec
            .From((int n) => n > 0)
            .AsAtLeastNSatisfied(1)
            .Create("is-positive");

        var spec = literal | modelCallback | resultCallback | multipleCallback;

        // Act
        var act = spec.IsSatisfiedBy([1]);

        // Assert
        act.Reason.ShouldBe("(is-positive == true) | (is-positive == true) | (is-positive == true) | (is-positive == true)");
    }

    [Fact]
    public void Should_yield_true_reason_when_overriding_assertion()
    {
        // Assemble
        var literal = Spec
            .From((int n) => n > 0)
            .WhenTrue("invalid")
            .WhenFalse("invalid")
            .Create("is positive");

        var modelCallback = Spec
            .From((int n) => n > 0)
            .WhenTrue(_ => "invalid")
            .WhenFalse("invalid")
            .Create("is positive");

        var resultCallback = Spec
            .From((int n) => n > 0)
            .WhenTrue((_, _) => "invalid")
            .WhenFalse("invalid")
            .Create("is positive");

        var multipleCallback = Spec
            .From((int n) => n > 0)
            .WhenTrueYield((_, _) => ["invalid"])
            .WhenFalse("invalid")
            .Create("is positive");

        var spec = literal | modelCallback | resultCallback | multipleCallback;

        // Act
        var act = spec.IsSatisfiedBy(1);

        // Assert
        act.Reason.ShouldBe("(is positive == true) | (is positive == true) | (is positive == true) | (is positive == true)");
    }

    [Fact]
    public void Should_yield_true_reason_when_overriding_higher_order_assertion()
    {
        // Assemble
        var literal = Spec
            .From((int n) => n > 0)
            .AsAnySatisfied()
            .WhenTrue("invalid")
            .WhenFalse("invalid")
            .Create("is positive");

        var modelCallback = Spec
            .From((int n) => n > 0)
            .AsAllSatisfied()
            .WhenTrue(_ => "invalid")
            .WhenFalse("invalid")
            .Create("is positive");

        var resultCallback = Spec
            .From((int n) => n > 0)
            .AsNSatisfied(1)
            .WhenTrue(_ => "is positive")
            .WhenFalse("invalid")
            .Create("is positive");

        var multipleCallback = Spec
            .From((int n) => n > 0)
            .AsAtLeastNSatisfied(1)
            .WhenTrueYield(_ => ["invalid"])
            .WhenFalse("invalid")
            .Create("is positive");

        var spec = literal | modelCallback | resultCallback | multipleCallback;

        // Act
        var act = spec.IsSatisfiedBy([1]);

        // Assert
        act.Reason.ShouldBe("(is positive == true) | (is positive == true) | (is positive == true) | (is positive == true)");
    }
}
