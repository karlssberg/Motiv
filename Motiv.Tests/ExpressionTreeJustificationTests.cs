namespace Motiv.Tests;

public class ExpressionTreeJustificationTests
{
    [Theory]
    [InlineData(-1,
        """
        ¬is-positive
            n <= 0
        """)]
    [InlineData(0,
        """
        ¬is-positive
            n <= 0
        """)]
    [InlineData(1,
        """
        is-positive
            n > 0
        """)]
    public void Should_justify_expressions(int model, string expectedResult)
    {
        // Arrange
        var sut =
            Spec.From((int n) => n > 0)
                .Create("is-positive");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Justification.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(-1,
        """
        n is not positive
            n <= 0
        """)]
    [InlineData(0,
        """
        n is not positive
            n <= 0
        """)]
    [InlineData(1,
        """
        n is positive
            n > 0
        """)]
    public void Should_include_both_custom_assertions_and_underlying_assertions_in_the_justification(
        int model,
        string expectedResult)
    {
        // Arrange
        var sut =
            Spec.From((int n) => n > 0)
                .WhenTrue("n is positive")
                .WhenFalse("n is not positive")
                .Create("is-positive");
        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Justification.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(-1,
        """
        ¬any-positive
            n is not positive
                n <= 0
        """)]
    [InlineData(0,
        """
        ¬any-positive
            n is not positive
                n <= 0
        """)]
    [InlineData(1,
        """
        any-positive
            n is positive
                n > 0
        """)]
    public void Should_include_underlying_assertions_in_the_justification_with_higher_order_propositions(int model, string expectedResult)
    {
        // Arrange
        var underlying = Spec.From((int n) => n > 0)
            .WhenTrue("n is positive")
            .WhenFalse("n is not positive")
            .Create("is-positive");

        var sut =
            Spec.Build(underlying)
                .AsAnySatisfied()
                .Create("any-positive");

        // Act
        var act = sut.IsSatisfiedBy([model]);

        // Assert
        act.Justification.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(
        """
        none positive
            -1 is not positive
                n <= 0
            -2 is not positive
                n <= 0
            -3 is not positive
                n <= 0
        """, -1, -2, -3)]
    [InlineData(
        """
        none positive
            0 is not positive
                n <= 0
            -1 is not positive
                n <= 0
            -2 is not positive
                n <= 0
        """, 0, -1, -2)]
    [InlineData(
        """
        some positive
            1 is positive
                n > 0
            2 is positive
                n > 0
        """, 0, 1, 2)]
    public void Should_include_both_custom_assertions_and_underlying_assertions_in_the_justification_with_higher_order_propositions(
        string expectedResult,
        params int[] model)
    {
        // Arrange
        var underlying =
            Spec.From((int n) => n > 0)
                .WhenTrue(n => $"{n} is positive")
                .WhenFalse(n => $"{n} is not positive")
                .Create("is-positive");

        var sut =
            Spec.Build(underlying)
                .AsAnySatisfied()
                .WhenTrue("some positive")
                .WhenFalse("none positive")
                .Create("any-positive");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Justification.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(
        """
        3x not positive
            n <= 0
        """, -1, -2, -3)]
    [InlineData(
        """
        3x not positive
            n <= 0
        """, 0, -1, -2)]
    [InlineData(
        """
        2x positive
            n > 0
        """, 0, 1, 2)]
    public void Should_justify_higher_order_expression_tree_spec(
        string expectedResult,
        params int[] model)
    {
        // Arrange
        var sut =
            Spec.From((int n) => n > 0)
                .AsAnySatisfied()
                .WhenTrue(eval => $"{eval.CausalCount}x positive")
                .WhenFalse(eval => $"{eval.CausalCount}x not positive")
                .Create("any positive");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Justification.Should().Be(expectedResult);
    }



    [Theory]
    [InlineData(
        """
        ¬should create guid
            ¬any positive
                none positive
                    n <= 0
        """, -1, -2, -3)]
    [InlineData(
        """
        should create guid
            any positive
                1 is positive
                    n > 0
        """, 1, 0, -1)]
    [InlineData(
        """
        should create guid
            any positive
                1 is positive
                2 is positive
                3 is positive
                    n > 0
        """, 1, 2, 3)]
    public void Should_insert_yielded_assertions_of_encapsulated_higher_order(
        string expectedResult,
        params int[] model)
    {
        // Arrange
        var anyPositive =
            Spec.From((decimal n) => n > 0)
                .AsAnySatisfied()
                .WhenTrueYield(eval => eval.CausalModels.Select(n => $"{n} is positive"))
                .WhenFalse("none positive")
                .Create("any positive")
                .ChangeModelTo((int[] n) => n.Select(Convert.ToDecimal));

        var sut =
            Spec.Build(anyPositive)
                .WhenTrue(Guid.NewGuid() as Guid?)
                .WhenFalse(default(Guid?))
                .Create("should create guid");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Justification.Should().Be(expectedResult);
    }


    [Theory]
    [InlineData("""
                any admins
                    isAdminResult == true
                        is admin
                """, "admin")]
    [InlineData("""
                ¬any admins
                    isAdminResult == false
                        is not admin
                """, "user")]
    public void Should_justify_any_linq_function_to_higher_order_proposition_when_boolean_result_is_returned(string expectedAssertion, string model)
    {
        // Assemble
        var isAdminResult =
            Spec.Build((string role) => role == "admin")
                .WhenTrue("is admin")
                .WhenFalse("is not admin")
                .Create("is-admin")
                .IsSatisfiedBy(model);

        var sut =
            Spec.From((IEnumerable<string> roles) => roles.Any(role => isAdminResult))
                .Create("any admins");

        // Act
        var act = sut.IsSatisfiedBy([model]);

        // Assert
        act.Justification.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(
        """
        all admins
            isAdminResult == true
                is admin
        """,
        "admin")]
    [InlineData(
        """
        ¬all admins
            isAdminResult == false
                is not admin
        """,
        "user")]
    public void Should_justify_all_linq_function_to_higher_order_proposition_when_boolean_result_is_returned(string expectedAssertion, string model)
    {
        // Assemble
        var isAdminResult =
            Spec.Build((string role) => role == "admin")
                .WhenTrue("is admin")
                .WhenFalse("is not admin")
                .Create("is-admin")
                .IsSatisfiedBy(model);

        var sut =
            Spec.From((IEnumerable<string> roles) => roles.All(role => isAdminResult))
                .Create("all admins");

        // Act
        var act = sut.IsSatisfiedBy([model]);

        // Assert
        act.Justification.Should().BeEquivalentTo(expectedAssertion);
    }
}
