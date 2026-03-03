namespace Motiv.Tests;

public class ExpressionTreeMetadataTests
{
    [Fact]
    public void Should_yield_true_assertion_when_overriding_assertion()
    {
        // Assemble
        var literal = Spec
            .From((int n) => n > 0)
            .WhenTrue(new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var modelCallback = Spec
            .From((int n) => n > 0)
            .WhenTrue(_ => new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var resultCallback = Spec
            .From((int n) => n > 0)
            .WhenTrue((_, _) => new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var multipleCallback = Spec
            .From((int n) => n > 0)
            .WhenTrueYield((_, _) => new Metadata[] {new("is positive")})
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var spec = literal | modelCallback | resultCallback | multipleCallback;

        // Act
        var act = spec.IsSatisfiedBy(1);

        // Assert
        act.Assertions.ShouldBe(["n > 0"]);
    }


    [Fact]
    public void Should_yield_true_assertion_when_overriding_assertion_from_higher_order_proposition()
    {
        // Assemble
        var literal = Spec
            .From((int n) => n > 0)
            .AsAnySatisfied()
            .WhenTrue(new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var modelCallback = Spec
            .From((int n) => n > 0)
            .AsAllSatisfied()
            .WhenTrue(_ => new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var resultCallback = Spec
            .From((int n) => n > 0)
            .AsNSatisfied(1)
            .WhenTrue(_ => new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var multipleCallback = Spec
            .From((int n) => n > 0)
            .AsAtLeastNSatisfied(1)
            .WhenTrueYield(_ => new Metadata[] {new("is positive")})
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var spec = literal | modelCallback | resultCallback | multipleCallback;

        // Act
        var act = spec.IsSatisfiedBy([1]);

        // Assert
        act.Assertions.ShouldBe(["n > 0"]);
    }

    [Fact]
    public void Should_yield_true_reason_when_not_overriding_assertion()
    {
        // Assemble
        var spec = Spec
            .From((int n) => n > 0)
            .Create("is-positive");

        // Act
        var act = spec.IsSatisfiedBy(1);

        // Assert
        act.Reason.ShouldBe("is-positive == true");
    }

    [Fact]
    public void Should_yield_true_reason_when_not_overriding_higher_order_assertion()
    {
        // Assemble
        var spec = Spec
            .From((int n) => n > 0)
            .AsAnySatisfied()
            .Create("is-positive");

        // Act
        var act = spec.IsSatisfiedBy([1]);

        // Assert
        act.Reason.ShouldBe("is-positive == true");
    }

    [Fact]
    public void Should_yield_true_reason_when_overriding_assertion()
    {
        // Assemble
        var literal = Spec
            .From((int n) => n > 0)
            .WhenTrue(new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var modelCallback = Spec
            .From((int n) => n > 0)
            .WhenTrue(_ => new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var resultCallback = Spec
            .From((int n) => n > 0)
            .WhenTrue((_, _) => new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var multipleCallback = Spec
            .From((int n) => n > 0)
            .WhenTrueYield((_, _) => new Metadata[] {new("is positive")})
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var spec = literal | modelCallback | resultCallback | multipleCallback;

        // Act
        var act = spec.IsSatisfiedBy(1);

        // Assert
        act.Reason.ShouldBe("(is-positive == true) | (is-positive == true) | (is-positive == true) | (is-positive == true)");
    }


    [Fact]
    public void Should_yield_true_reason_when_overriding_higher_order_assertion()
    {
        // Assemble
        var literal = Spec
            .From((int n) => n > 0)
            .AsAnySatisfied()
            .WhenTrue(new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var modelCallback = Spec
            .From((int n) => n > 0)
            .AsAllSatisfied()
            .WhenTrue(_ => new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var resultCallback = Spec
            .From((int n) => n > 0)
            .AsNSatisfied(1)
            .WhenTrue(_ => new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var multipleCallback = Spec
            .From((int n) => n > 0)
            .AsAtLeastNSatisfied(1)
            .WhenTrueYield(_ => new Metadata[] {new("is positive")})
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var spec = literal | modelCallback | resultCallback | multipleCallback;

        // Act
        var act = spec.IsSatisfiedBy([1]);

        // Assert
        act.Reason.ShouldBe("(is-positive == true) | (is-positive == true) | (is-positive == true) | (is-positive == true)");
    }

    [Fact]
    public void Should_yield_true_justification_when_overriding_assertion()
    {
        // Assemble
        var literal = Spec
            .From((int n) => n > 0)
            .WhenTrue(new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var modelCallback = Spec
            .From((int n) => n > 0)
            .WhenTrue(_ => new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var resultCallback = Spec
            .From((int n) => n > 0)
            .WhenTrue((_, _) => new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var multipleCallback = Spec
            .From((int n) => n > 0)
            .WhenTrueYield((_, _) => new Metadata[] {new("is positive")})
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var spec = literal | modelCallback | resultCallback | multipleCallback;

        // Act
        var act = spec.IsSatisfiedBy(1);

        // Assert
        act.Justification.ShouldBe(
            """
            OR
                is-positive == true
                    (int n) => n > 0 == true
                        n > 0
                is-positive == true
                    (int n) => n > 0 == true
                        n > 0
                is-positive == true
                    (int n) => n > 0 == true
                        n > 0
                is-positive == true
                    (int n) => n > 0 == true
                        n > 0
            """);
    }

    [Fact]
    public void Should_yield_true_justification_when_overriding_higher_order_assertion()
    {
        // Assemble
        var literal = Spec
            .From((int n) => n > 0)
            .AsAnySatisfied()
            .WhenTrue(new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var modelCallback = Spec
            .From((int n) => n > 0)
            .AsAllSatisfied()
            .WhenTrue(_ => new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var resultCallback = Spec
            .From((int n) => n > 0)
            .AsNSatisfied(1)
            .WhenTrue(_ => new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var multipleCallback = Spec
            .From((int n) => n > 0)
            .AsAtLeastNSatisfied(1)
            .WhenTrueYield(_ => new Metadata[] {new("is positive")})
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var spec = literal | modelCallback | resultCallback | multipleCallback;

        // Act
        var act = spec.IsSatisfiedBy([1]);

        // Assert
        act.Justification.ShouldBe(
            """
            OR
                is-positive == true
                    (int n) => n > 0 == true (1)
                        n > 0
                is-positive == true
                    (int n) => n > 0 == true (1)
                        n > 0
                is-positive == true
                    (int n) => n > 0 == true (1)
                        n > 0
                is-positive == true
                    (int n) => n > 0 == true (1)
                        n > 0
            """);
    }

    [Fact]
    public void Should_yield_false_assertion_when_overriding_assertion()
    {
        // Assemble
        var literal = Spec
            .From((int n) => n > 0)
            .WhenTrue(new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var modelCallback = Spec
            .From((int n) => n > 0)
            .WhenTrue(new Metadata("is positive"))
            .WhenFalse(_ => new Metadata("is not positive"))
            .Create("is-positive");

        var resultCallback = Spec
            .From((int n) => n > 0)
            .WhenTrue(new Metadata("is positive"))
            .WhenFalse((_, _) => new Metadata("is not positive"))
            .Create("is-positive");

        var multipleCallback = Spec
            .From((int n) => n > 0)
            .WhenTrue(new Metadata("is positive"))
            .WhenFalseYield((_, _) => [new Metadata("is not positive")])
            .Create("is-positive");

        var spec = literal | modelCallback | resultCallback | multipleCallback;

        // Act
        var act = spec.IsSatisfiedBy(-1);

        // Assert
        act.Assertions.ShouldBe(["n <= 0"]);
    }

    [Fact]
    public void Should_yield_false_assertion_when_true_multiple_assertions()
    {
        // Assemble
        var literal = Spec
            .From((int n) => n > 0)
            .WhenTrueYield((_, _) => new [] { new Metadata("is positive") })
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var modelCallback = Spec
            .From((int n) => n > 0)
            .WhenTrueYield((_, _) => new [] { new Metadata("is positive") })
            .WhenFalse(_ => new Metadata("is not positive"))
            .Create("is-positive");

        var resultCallback = Spec
            .From((int n) => n > 0)
            .WhenTrueYield((_, _) => new [] { new Metadata("is positive") })
            .WhenFalse((_, _) => new Metadata("is not positive"))
            .Create("is-positive");

        var multipleCallback = Spec
            .From((int n) => n > 0)
            .WhenTrueYield((_, _) => new [] { new Metadata("is positive") })
            .WhenFalseYield((_, _) => [new Metadata("is not positive")])
            .Create("is-positive");

        var spec = literal | modelCallback | resultCallback | multipleCallback;

        // Act
        var act = spec.IsSatisfiedBy(-1);

        // Assert
        act.Assertions.ShouldBe(["n <= 0"]);
    }

    [Fact]
    public void Should_yield_false_assertion_when_overriding_higher_order_assertion()
    {
        // Assemble
        var literal = Spec
            .From((int n) => n > 0)
            .AsAnySatisfied()
            .WhenTrue(new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var resultCallback = Spec
            .From((int n) => n > 0)
            .AsNSatisfied(1)
            .WhenTrue(new Metadata("is positive"))
            .WhenFalse(_ => new Metadata("is not positive"))
            .Create("is-positive");

        var multipleCallback = Spec
            .From((int n) => n > 0)
            .AsAtLeastNSatisfied(1)
            .WhenTrue(new Metadata("is positive"))
            .WhenFalseYield(_ => [new Metadata("is not positive")])
            .Create("is-positive");

        var spec = literal | resultCallback | multipleCallback;

        // Act
        var act = spec.IsSatisfiedBy([-1]);

        // Assert
        act.Assertions.ShouldBe(["n <= 0"]);
    }

    [Fact]
    public void Should_yield_false_reason_when_not_overriding_assertion()
    {
        // Assemble
        var spec = Spec
            .From((int n) => n > 0)
            .Create("is-positive");

        // Act
        var act = spec.IsSatisfiedBy(-1);

        // Assert
        act.Reason.ShouldBe("is-positive == false");
    }


    [Fact]
    public void Should_yield_false_reason_when_not_overriding_higher_order_assertion()
    {
        // Assemble
        var spec = Spec
            .From((int n) => n > 0)
            .AsAnySatisfied()
            .Create("is-positive");

        // Act
        var act = spec.IsSatisfiedBy([-1]);

        // Assert
        act.Reason.ShouldBe("is-positive == false");
    }

    [Fact]
    public void Should_yield_false_reason_when_overriding_assertion()
    {
        // Assemble
        var literal = Spec
            .From((int n) => n > 0)
            .WhenTrue(new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var modelCallback = Spec
            .From((int n) => n > 0)
            .WhenTrue(new Metadata("is positive"))
            .WhenFalse(_ => new Metadata("is not positive"))
            .Create("is-positive");

        var resultCallback = Spec
            .From((int n) => n > 0)
            .WhenTrue(new Metadata("is positive"))
            .WhenFalse((_, _) => new Metadata("is not positive"))
            .Create("is-positive");

        var multipleCallback = Spec
            .From((int n) => n > 0)
            .WhenTrue(new Metadata("is positive"))
            .WhenFalseYield((_, _) => [new Metadata("is not positive")])
            .Create("is-positive");

        var spec = literal | modelCallback | resultCallback | multipleCallback;

        // Act
        var act = spec.IsSatisfiedBy(-1);

        // Assert
        act.Reason.ShouldBe("(is-positive == false) | (is-positive == false) | (is-positive == false) | (is-positive == false)");
    }

    [Fact]
    public void Should_yield_false_reason_when_overriding_higher_order_assertion()
    {
        // Assemble
        var literal = Spec
            .From((int n) => n > 0)
            .AsAnySatisfied()
            .WhenTrue(new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var modelCallback = Spec
            .From((int n) => n > 0)
            .AsAllSatisfied()
            .WhenTrue(new Metadata("is positive"))
            .WhenFalse(_ => new Metadata("is not positive"))
            .Create("is-positive");

        var resultCallback = Spec
            .From((int n) => n > 0)
            .AsNSatisfied(1)
            .WhenTrue(new Metadata("is positive"))
            .WhenFalse(_ => new Metadata("is not positive"))
            .Create("is-positive");

        var multipleCallback = Spec
            .From((int n) => n > 0)
            .AsAtLeastNSatisfied(1)
            .WhenTrue(new Metadata("is positive"))
            .WhenFalseYield(_ => [new Metadata("is not positive")])
            .Create("is-positive");

        var spec = literal | modelCallback | resultCallback | multipleCallback;

        // Act
        var act = spec.IsSatisfiedBy([-1]);

        // Assert
        act.Reason.ShouldBe("(is-positive == false) | (is-positive == false) | (is-positive == false) | (is-positive == false)");
    }

    [Fact]
    public void Should_yield_false_justification_when_overriding_assertion()
    {
        // Assemble
        var literal = Spec
            .From((int n) => n > 0)
            .WhenTrue(new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var modelCallback = Spec
            .From((int n) => n > 0)
            .WhenTrue(new Metadata("is positive"))
            .WhenFalse(_ => new Metadata("is not positive"))
            .Create("is-positive");

        var resultCallback = Spec
            .From((int n) => n > 0)
            .WhenTrue(new Metadata("is positive"))
            .WhenFalse((_, _) => new Metadata("is not positive"))
            .Create("is-positive");

        var multipleCallback = Spec
            .From((int n) => n > 0)
            .WhenTrue(new Metadata("is positive"))
            .WhenFalseYield((_, _) => [new Metadata("is not positive")])
            .Create("is-positive");

        var spec = literal | modelCallback | resultCallback | multipleCallback;

        // Act
        var act = spec.IsSatisfiedBy(-1);

        // Assert
        act.Justification.ShouldBe(
            """
            OR
                is-positive == false
                    (int n) => n > 0 == false
                        n <= 0
                is-positive == false
                    (int n) => n > 0 == false
                        n <= 0
                is-positive == false
                    (int n) => n > 0 == false
                        n <= 0
                is-positive == false
                    (int n) => n > 0 == false
                        n <= 0
            """);
    }

    [Fact]
    public void Should_yield_false_justification_when_overriding_higher_order_assertion()
    {
        // Assemble
        var literal = Spec
            .From((int n) => n > 0)
            .AsAnySatisfied()
            .WhenTrue(new Metadata("is positive"))
            .WhenFalse(new Metadata("is not positive"))
            .Create("is-positive");

        var modelCallback = Spec
            .From((int n) => n > 0)
            .AsAllSatisfied()
            .WhenTrue(new Metadata("is positive"))
            .WhenFalse(_ => new Metadata("is not positive"))
            .Create("is-positive");

        var resultCallback = Spec
            .From((int n) => n > 0)
            .AsNSatisfied(1)
            .WhenTrue(new Metadata("is positive"))
            .WhenFalse(_ => new Metadata("is not positive"))
            .Create("is-positive");

        var multipleCallback = Spec
            .From((int n) => n > 0)
            .AsAtLeastNSatisfied(1)
            .WhenTrue(new Metadata("is positive"))
            .WhenFalseYield(_ => [new Metadata("is not positive")])
            .Create("is-positive");

        var spec = literal | modelCallback | resultCallback | multipleCallback;

        // Act
        var act = spec.IsSatisfiedBy([-1]);

        // Assert
        act.Justification.ShouldBe(
            """
            OR
                is-positive == false
                    (int n) => n > 0 == false (1)
                        n <= 0
                is-positive == false
                    (int n) => n > 0 == false (1)
                        n <= 0
                is-positive == false
                    (int n) => n > 0 == false (1)
                        n <= 0
                is-positive == false
                    (int n) => n > 0 == false (1)
                        n <= 0
            """);
    }

    [Fact]
    public void Should_assert_why_a_higher_order_predicate_was_unsatisfied()
    {
        var areAllInRange =
            Spec.From((ICollection<int> numbers) => numbers.All(n => n > 0 & n <= 10))
                .Create("all in range");

        var result = areAllInRange.IsSatisfiedBy([-1, 2, 3]);

        result.Assertions.ShouldBe(["n <= 0"]);
    }



    [Fact]
    public void Should_assert_why_a_nested_higher_order_predicate_was_unsatisfied()
    {
        var areAllInRange =
            Spec.From((ICollection<int> numbers) => numbers.All(n => n > 0 & n <= 10))
                .AsAnySatisfied()
                .Create("all in range");

        var result = areAllInRange.IsSatisfiedBy([[-1, 2, 3]]);

        result.Assertions.ShouldBe(["n <= 0"]);
    }

    public record Metadata(string Assertion)
    {
        public string Assertion { get; } = Assertion;
    }
}

