using Shouldly;

namespace Motiv.Tests;

public class ExpressionTreeJustificationTests
{
    [Theory]
    [InlineData(-1,
        """
        ¬is-positive
            (int n) => n > 0 == false
                n <= 0
        """)]
    [InlineData(0,
        """
        ¬is-positive
            (int n) => n > 0 == false
                n <= 0
        """)]
    [InlineData(1,
        """
        is-positive
            (int n) => n > 0 == true
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
        act.Justification.ShouldBe(expectedResult);
    }

    [Theory]
    [InlineData(-1,
        """
        n is not positive
            (int n) => n > 0 == false
                n <= 0
        """)]
    [InlineData(0,
        """
        n is not positive
            (int n) => n > 0 == false
                n <= 0
        """)]
    [InlineData(1,
        """
        n is positive
            (int n) => n > 0 == true
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
        act.Justification.ShouldBe(expectedResult);
    }

    [Theory]
    [InlineData(-1,
        """
        ¬any-positive
            n is not positive
                (int n) => n > 0 == false
                    n <= 0
        """)]
    [InlineData(0,
        """
        ¬any-positive
            n is not positive
                (int n) => n > 0 == false
                    n <= 0
        """)]
    [InlineData(1,
        """
        any-positive
            n is positive
                (int n) => n > 0 == true
                    n > 0
        """)]
    public void Should_include_underlying_assertions_in_the_justification_with_higher_order_propositions(int model, string expectedResult)
    {
        // Arrange
        var underlying =
            Spec.From((int n) => n > 0)
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
        act.Justification.ShouldBe(expectedResult);
    }

    [Theory]
    [InlineData(
        """
        none positive
            -1 is not positive
                (int n) => n > 0 == false
                    n <= 0
            -2 is not positive
                (int n) => n > 0 == false
                    n <= 0
            -3 is not positive
                (int n) => n > 0 == false
                    n <= 0
        """, -1, -2, -3)]
    [InlineData(
        """
        none positive
            0 is not positive
                (int n) => n > 0 == false
                    n <= 0
            -1 is not positive
                (int n) => n > 0 == false
                    n <= 0
            -2 is not positive
                (int n) => n > 0 == false
                    n <= 0
        """, 0, -1, -2)]
    [InlineData(
        """
        some positive
            1 is positive
                (int n) => n > 0 == true
                    n > 0
            2 is positive
                (int n) => n > 0 == true
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
        act.Justification.ShouldBe(expectedResult);
    }



    [Theory]
    [InlineData(
        """
        none positive
            -1 is not positive
                (int n) => n > 0 == false
                    n <= 0
            -2 is not positive
                (int n) => n > 0 == false
                    n <= 0
            -3 is not positive
                (int n) => n > 0 == false
                    n <= 0
        """, -1, -2, -3)]
    [InlineData(
        """
        none positive
            0 is not positive
                (int n) => n > 0 == false
                    n <= 0
            -1 is not positive
                (int n) => n > 0 == false
                    n <= 0
            -2 is not positive
                (int n) => n > 0 == false
                    n <= 0
        """, 0, -1, -2)]
    [InlineData(
        """
        some positive
            1 is positive
                (int n) => n > 0 == true
                    n > 0
            2 is positive
                (int n) => n > 0 == true
                    n > 0
        """, 0, 1, 2)]
    public void Should_include_both_custom_assertions_and_underlying_assertions_in_the_justification_with_higher_order_propositions_created_without_supplying_a_statement(
        string expectedResult,
        params int[] model)
    {
        // Arrange
        var underlying =
            Spec.From((int n) => n > 0)
                .WhenTrue(n => $"{n} is positive")
                .WhenFalse(n => $"{n} is not positive")
                .Create();

        var sut =
            Spec.Build(underlying)
                .AsAnySatisfied()
                .WhenTrue("some positive")
                .WhenFalse("none positive")
                .Create("any-positive");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Justification.ShouldBe(expectedResult);
    }

    [Theory]
    [InlineData(
        """
        3x not positive
            (int n) => n > 0 == false
                n <= 0
        """, -1, -2, -3)]
    [InlineData(
        """
        3x not positive
            (int n) => n > 0 == false
                n <= 0
        """, 0, -1, -2)]
    [InlineData(
        """
        2x positive
            (int n) => n > 0 == true
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
        act.Justification.ShouldBe(expectedResult);
    }

    [Theory]
    [InlineData(
        """
        ¬should create guid
            ¬any positive
                none positive
                    (decimal n) => n > 0 == false
                        n <= 0
        """, -1, -2, -3)]
    [InlineData(
        """
        should create guid
            any positive
                1 is positive
                    (decimal n) => n > 0 == true
                        n > 0
        """, 1, 0, -1)]
    [InlineData(
        """
        should create guid
            any positive
                1 is positive
                2 is positive
                3 is positive
                    (decimal n) => n > 0 == true
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
        act.Justification.ShouldBe(expectedResult);
    }

    [Theory]
    [InlineData("""
                any admins
                    (IEnumerable<string> roles) => roles.Any((string role) => role == "admin") == true
                        roles.Any((string role) => role == "admin") == true
                            (string role) => role == "admin" == true
                                role == "admin"
                """, "admin")]
    [InlineData("""
                ¬any admins
                    (IEnumerable<string> roles) => roles.Any((string role) => role == "admin") == false
                        roles.Any((string role) => role == "admin") == false
                            (string role) => role == "admin" == false
                                role != "admin"
                """, "user")]
    public void Should_justify_any_linq_function_to_higher_order_proposition_when_boolean_is_returned(string expectedAssertion, string model)
    {
        // Assemble
        var sut =
            Spec.From((IEnumerable<string> roles) => roles.Any(role => role == "admin"))
                .Create("any admins");

        // Act
        var act = sut.IsSatisfiedBy([model]);

        // Assert
        act.Justification.ShouldBe(expectedAssertion);
    }

    [Theory]
    [InlineData("""
                any admins
                    (IEnumerable<string> roles) => roles.Any((string role) => isAdminResult) == true
                        roles.Any((string role) => isAdminResult) == true
                            (string role) => isAdminResult == true
                                is admin
                """, "admin")]
    [InlineData("""
                ¬any admins
                    (IEnumerable<string> roles) => roles.Any((string role) => isAdminResult) == false
                        roles.Any((string role) => isAdminResult) == false
                            (string role) => isAdminResult == false
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
        act.Justification.ShouldBe(expectedAssertion);
    }

    [Theory]
    [InlineData(
        """
        any admins or super users
            (IEnumerable<string> roles) => roles.Any((string role) => isSuperUser || isAdminResult) == true
                roles.Any((string role) => isSuperUser || isAdminResult) == true
                    (string role) => (isSuperUser || isAdminResult) == true
                        OR
                            is admin
        """,
        "admin")]
    [InlineData(
        """
        any admins or super users
            (IEnumerable<string> roles) => roles.Any((string role) => isSuperUser || isAdminResult) == true
                roles.Any((string role) => isSuperUser || isAdminResult) == true
                    (string role) => (isSuperUser || isAdminResult) == true
                        is super user
        """,
        "superuser")]
    [InlineData(
        """
        ¬any admins or super users
            (IEnumerable<string> roles) => roles.Any((string role) => isSuperUser || isAdminResult) == false
                roles.Any((string role) => isSuperUser || isAdminResult) == false
                    (string role) => (isSuperUser || isAdminResult) == false
                        OR
                            is not super user
                            is not admin
        """,
        "user")]
    public void Should_justify_any_linq_function_to_higher_order_proposition_when_multiple_boolean_results_are_returned(string expectedAssertion, string model)
    {
        // Assemble
        var isAdminResult =
            Spec.Build((string role) => role == "admin")
                .WhenTrue("is admin")
                .WhenFalse("is not admin")
                .Create("is-admin")
                .IsSatisfiedBy(model);

        var isSuperUser =
            Spec.Build((string role) => role == "superuser")
                .WhenTrue("is super user")
                .WhenFalse("is not super user")
                .Create("is-super-user")
                .IsSatisfiedBy(model);

        var sut =
            Spec.From((IEnumerable<string> roles) => roles.Any(role => isSuperUser || isAdminResult))
                .Create("any admins or super users");

        // Act
        var act = sut.IsSatisfiedBy([model]);

        // Assert
        act.Justification.ShouldBe(expectedAssertion);
    }

    [Theory]
    [InlineData(
        """
        any admins or super users
            (IEnumerable<string> roles) => roles.Any(isSuperUser | isAdminResult) == true
                roles.Any(isSuperUser | isAdminResult) == true
                    OR
                        is admin
        """,
        "admin")]
    [InlineData(
        """
        any admins or super users
            (IEnumerable<string> roles) => roles.Any(isSuperUser | isAdminResult) == true
                roles.Any(isSuperUser | isAdminResult) == true
                    OR
                        is super user
        """,
        "superuser")]
    [InlineData(
        """
        ¬any admins or super users
            (IEnumerable<string> roles) => roles.Any(isSuperUser | isAdminResult) == false
                roles.Any(isSuperUser | isAdminResult) == false
                    OR
                        is not super user
                        is not admin
        """,
        "user")]
    public void Should_justify_any_linq_function_to_higher_order_proposition_when_multiple_specs_are_returned(string expectedAssertion, string model)
    {
        // Assemble
        var isAdminResult =
            Spec.Build((string role) => role == "admin")
                .WhenTrue("is admin")
                .WhenFalse("is not admin")
                .Create("is-admin");

        var isSuperUser =
            Spec.Build((string role) => role == "superuser")
                .WhenTrue("is super user")
                .WhenFalse("is not super user")
                .Create("is-super-user");

        var sut =
            Spec.From((IEnumerable<string> roles) => roles.Any(isSuperUser | isAdminResult))
                .Create("any admins or super users");

        // Act
        var act = sut.IsSatisfiedBy([model]);

        // Assert
        act.Justification.ShouldBe(expectedAssertion);
    }

    [Theory]
    [InlineData(
        """
        all admins
            (IEnumerable<string> roles) => roles.All((string role) => isAdminResult) == true
                roles.All((string role) => isAdminResult) == true
                    (string role) => isAdminResult == true
                        is admin
        """,
        "admin")]
    [InlineData(
        """
        ¬all admins
            (IEnumerable<string> roles) => roles.All((string role) => isAdminResult) == false
                roles.All((string role) => isAdminResult) == false
                    (string role) => isAdminResult == false
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
        act.Justification.ShouldBe(expectedAssertion);
    }

    [Theory]
    [InlineData(
        """
        all admins or super users
            (IEnumerable<string> roles) => roles.All((string role) => isSuperUser || isAdminResult) == true
                roles.All((string role) => isSuperUser || isAdminResult) == true
                    (string role) => (isSuperUser || isAdminResult) == true
                        OR
                            is admin
        """,
        "admin")]
    [InlineData(
        """
        all admins or super users
            (IEnumerable<string> roles) => roles.All((string role) => isSuperUser || isAdminResult) == true
                roles.All((string role) => isSuperUser || isAdminResult) == true
                    (string role) => (isSuperUser || isAdminResult) == true
                        is super user
        """,
        "superuser")]
    [InlineData(
        """
        ¬all admins or super users
            (IEnumerable<string> roles) => roles.All((string role) => isSuperUser || isAdminResult) == false
                roles.All((string role) => isSuperUser || isAdminResult) == false
                    (string role) => (isSuperUser || isAdminResult) == false
                        OR
                            is not super user
                            is not admin
        """,
        "user")]
    public void Should_justify_all_linq_function_to_higher_order_proposition_when_multiple_boolean_results_are_returned(string expectedAssertion, string model)
    {
        // Assemble
        var isAdminResult =
            Spec.Build((string role) => role == "admin")
                .WhenTrue("is admin")
                .WhenFalse("is not admin")
                .Create("is-admin")
                .IsSatisfiedBy(model);

        var isSuperUser =
            Spec.Build((string role) => role == "superuser")
                .WhenTrue("is super user")
                .WhenFalse("is not super user")
                .Create("is-super-user")
                .IsSatisfiedBy(model);

        var sut =
            Spec.From((IEnumerable<string> roles) => roles.All(role => isSuperUser || isAdminResult))
                .Create("all admins or super users");

        // Act
        var act = sut.IsSatisfiedBy([model]);

        // Assert
        act.Justification.ShouldBe(expectedAssertion);
    }

    [Theory]
    [InlineData(
        """
        all admins or super users
            (IEnumerable<string> roles) => roles.All(isSuperUser | isAdminResult) == true
                roles.All(isSuperUser | isAdminResult) == true
                    OR
                        is admin
        """,
        "admin")]
    [InlineData(
        """
        all admins or super users
            (IEnumerable<string> roles) => roles.All(isSuperUser | isAdminResult) == true
                roles.All(isSuperUser | isAdminResult) == true
                    OR
                        is super user
        """,
        "superuser")]
    [InlineData(
        """
        ¬all admins or super users
            (IEnumerable<string> roles) => roles.All(isSuperUser | isAdminResult) == false
                roles.All(isSuperUser | isAdminResult) == false
                    OR
                        is not super user
                        is not admin
        """,
        "user")]
    public void Should_justify_all_linq_function_to_higher_order_proposition_when_multiple_specs_are_returned(string expectedAssertion, string model)
    {
        // Assemble
        var isAdminResult =
            Spec.Build((string role) => role == "admin")
                .WhenTrue("is admin")
                .WhenFalse("is not admin")
                .Create("is-admin");

        var isSuperUser =
            Spec.Build((string role) => role == "superuser")
                .WhenTrue("is super user")
                .WhenFalse("is not super user")
                .Create("is-super-user");

        var sut =
            Spec.From((IEnumerable<string> roles) => roles.All(isSuperUser | isAdminResult))
                .Create("all admins or super users");

        // Act
        var act = sut.IsSatisfiedBy([model]);

        // Assert
        act.Justification.ShouldBe(expectedAssertion);
    }

    [Fact]
    public void Should_justify_all_operation_when_using_subtypes_of_ienumerable()
    {
        var allPositive =
            Spec.From((ICollection<int> numbers) => numbers.All(n => n > 0))
                .Create("all positive");

        var result = allPositive.IsSatisfiedBy([-1, 2, 3]);

        result.Justification.ShouldBe(
            """
            ¬all positive
                (ICollection<int> numbers) => numbers.All((int n) => n > 0) == false
                    numbers.All((int n) => n > 0) == false
                        (int n) => n > 0 == false
                            n <= 0
            """);
    }

    [Fact]
    public void Should_justify_any_operation_when_using_subtypes_of_ienumerable()
    {
        var allPositive =
            Spec.From((ICollection<int> numbers) => numbers.Any(n => n > 0))
                .Create("any positive");

        var result = allPositive.IsSatisfiedBy([-1, 2, 3]);

        result.Justification.ShouldBe(
            """
            any positive
                (ICollection<int> numbers) => numbers.Any((int n) => n > 0) == true
                    numbers.Any((int n) => n > 0) == true
                        (int n) => n > 0 == true
                            n > 0
            """);
    }

    [Fact]
    public void Should_justify_all_operation_when_using_an_array()
    {
        var allPositive =
            Spec.From((int[] numbers) => numbers.All(n => n > 0) )
                .Create("all positive");

        var result = allPositive.IsSatisfiedBy([-1, 2, 3]);

        result.Justification.ShouldBe(
            """
            ¬all positive
                (int[] numbers) => numbers.All((int n) => n > 0) == false
                    numbers.All((int n) => n > 0) == false
                        (int n) => n > 0 == false
                            n <= 0
            """);
    }

    [Fact]
    public void Should_justify_any_operation_when_using_an_array()
    {
        var allPositive =
            Spec.From((int[] numbers) => numbers.Any(n => n > 0))
                .Create("any positive");

        var result = allPositive.IsSatisfiedBy([-1, 2, 3]);

        result.Justification.ShouldBe(
            """
            any positive
                (int[] numbers) => numbers.Any((int n) => n > 0) == true
                    numbers.Any((int n) => n > 0) == true
                        (int n) => n > 0 == true
                            n > 0
            """);
    }

    [Theory]
    [InlineData("""
                is admin
                    (ICollection<string> users) => (users.Any((string user) => user == "root") || users.Count == 1) == true
                        OR
                            users.Any((string user) => user == "root") == true
                                (string user) => user == "root" == true
                                    user == "root"
                """, "root")]
    [InlineData("""
                is admin
                    (ICollection<string> users) => (users.Any((string user) => user == "root") || users.Count == 1) == true
                        OR
                            users.Count == 1
                """, "user")]
    [InlineData("""
                ¬is admin
                    (ICollection<string> users) => (users.Any((string user) => user == "root") || users.Count == 1) == false
                        OR
                            users.Any((string user) => user == "root") == false
                                (string user) => user == "root" == false
                                    user != "root"
                            users.Count != 1
                """, "user", "super-user")]
    public void Should_justify_multiple_clause_expressions(string expectedAssertion, params string[] model)
    {
        // Assemble
        var sut =
            Spec.From((ICollection<string> users) => users.Any(user => user == "root") || users.Count == 1)
                .Create("is admin");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Justification.ShouldBe(expectedAssertion);
    }
}
