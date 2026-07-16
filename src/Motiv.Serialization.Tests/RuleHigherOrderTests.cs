using static Motiv.Serialization.Tests.SpecAssertions;

namespace Motiv.Serialization.Tests;

public class RuleHigherOrderTests
{
    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0)
            .WhenTrue("is positive")
            .WhenFalse("is not positive")
            .Create();

    private static RuleSerializer CreateSerializer() =>
        new(new SpecRegistry().Register("is-positive", IsPositive));

    [Fact]
    public void Should_load_a_named_all_satisfied_node_like_its_fluent_equivalent()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAllSatisfied": { "spec": "is-positive" }, "name": "all positive" } }""";
        var expected = Spec
            .Build(IsPositive)
            .AsAllSatisfied()
            .Create("all positive");

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, 2 }, new[] { 1, -2 }, new[] { -1, -2 });
    }

    [Fact]
    public void Should_load_a_decorated_unnamed_any_satisfied_node_like_its_fluent_equivalent()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "asAnySatisfied": { "spec": "is-positive" },
                "whenTrue": "some are positive",
                "whenFalse": "none are positive"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .AsAnySatisfied()
            .WhenTrue("some are positive")
            .WhenFalse("none are positive")
            .Create();

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, -2 }, new[] { -1, -2 });
    }

    [Fact]
    public void Should_load_a_decorated_named_higher_order_node_like_its_fluent_equivalent()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "asAllSatisfied": { "spec": "is-positive" },
                "whenTrue": "all positive",
                "whenFalse": "some are not positive",
                "name": "positivity"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .AsAllSatisfied()
            .WhenTrue("all positive")
            .WhenFalse("some are not positive")
            .Create("positivity");

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, 2 }, new[] { 1, -2 });
    }

    [Fact]
    public void Should_load_an_exactly_n_satisfied_node_with_a_literal_n()
    {
        // Arrange
        const string json =
            """{ "rule": { "asNSatisfied": { "spec": "is-positive" }, "n": 2, "name": "exactly two" } }""";
        var expected = Spec
            .Build(IsPositive)
            .AsNSatisfied(2)
            .Create("exactly two");

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, 2, -3 }, new[] { 1, 2, 3 }, new[] { -1, -2 });
    }

    [Fact]
    public void Should_resolve_n_from_a_parameter_reference()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": { "minOrders": { "type": "integer" } },
              "rule": { "asAtLeastNSatisfied": { "spec": "is-positive" }, "n": "@minOrders", "name": "quota" }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .AsAtLeastNSatisfied(2)
            .Create("quota");

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>>(json, new { minOrders = 2 });

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, 2, -3 }, new[] { 1, -2, -3 });
    }

    [Fact]
    public void Should_load_an_at_most_n_satisfied_node()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAtMostNSatisfied": { "spec": "is-positive" }, "n": 1, "name": "at most one" } }""";
        var expected = Spec
            .Build(IsPositive)
            .AsAtMostNSatisfied(1)
            .Create("at most one");

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, -2 }, new[] { 1, 2 });
    }

    [Fact]
    public void Should_load_a_composed_inner_tree_inside_a_higher_order_node()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "asAllSatisfied": { "and": [ { "spec": "is-positive" }, { "not": { "spec": "is-positive" } } ] },
                "name": "contradiction everywhere"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive.And(IsPositive.Not()))
            .AsAllSatisfied()
            .Create("contradiction everywhere");

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, 2 }, new[] { -1 });
    }

    [Fact]
    public void Should_reanchor_to_a_concrete_collection_model_type()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAllSatisfied": { "spec": "is-positive" }, "name": "all positive" } }""";
        var expected = Spec
            .Build(IsPositive)
            .AsAllSatisfied()
            .Create("all positive")
            .ChangeModelTo<int[]>(models => models);

        // Act
        var loaded = CreateSerializer().Deserialize<int[]>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, 2 }, new[] { 1, -2 });
    }

    [Fact]
    public void Should_report_a_model_that_is_not_a_collection()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAllSatisfied": { "spec": "is-positive" }, "name": "all positive" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidHigherOrderPath);
        error.Path.ShouldBe("$.rule");
    }

    [Fact]
    public void Should_report_an_unknown_parameter_reference_in_n()
    {
        // Arrange
        const string json =
            """{ "rule": { "asNSatisfied": { "spec": "is-positive" }, "n": "@missing", "name": "x" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<IEnumerable<int>>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.UnknownParameterReference);
        error.Path.ShouldBe("$.rule.n");
    }

    [Fact]
    public void Should_report_a_non_integer_parameter_referenced_by_n()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": { "label": { "type": "string", "default": "x" } },
              "rule": { "asNSatisfied": { "spec": "is-positive" }, "n": "@label", "name": "x" }
            }
            """;

        // Act
        var act = () => CreateSerializer().Deserialize<IEnumerable<int>>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.ParameterTypeMismatch);
        error.Path.ShouldBe("$.rule.n");
    }

    [Fact]
    public void Should_select_the_collection_through_a_single_segment_path()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAllSatisfied": { "spec": "is-positive" }, "path": "Orders", "name": "all orders positive" } }""";
        var expected = Spec
            .Build(IsPositive)
            .AsAllSatisfied()
            .Create("all orders positive")
            .ChangeModelTo<Customer>(customer => customer.Orders);

        // Act
        var loaded = CreateSerializer().Deserialize<Customer>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected,
            new Customer { Orders = [1, 2] },
            new Customer { Orders = [1, -2] });
    }

    [Fact]
    public void Should_select_the_collection_through_a_nested_path()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "asAnySatisfied": { "spec": "is-positive" },
                "path": "Account.Orders",
                "name": "an account order is positive"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .AsAnySatisfied()
            .Create("an account order is positive")
            .ChangeModelTo<Customer>(customer => customer.Account.Orders);

        // Act
        var loaded = CreateSerializer().Deserialize<Customer>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected,
            new Customer { Account = new Account { Orders = [-1, 2] } },
            new Customer { Account = new Account { Orders = [-1, -2] } });
    }

    [Fact]
    public void Should_report_a_path_segment_that_is_not_a_public_property()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAllSatisfied": { "spec": "is-positive" }, "path": "Nope", "name": "x" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<Customer>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidHigherOrderPath);
        error.Path.ShouldBe("$.rule.path");
        error.Message.ShouldContain("Nope");
    }

    [Fact]
    public void Should_report_a_path_that_selects_a_non_collection()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAllSatisfied": { "spec": "is-positive" }, "path": "Age", "name": "x" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<Customer>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidHigherOrderPath);
        error.Path.ShouldBe("$.rule.path");
    }

    [Fact]
    public void Should_propagate_a_failed_inner_spec_through_a_bare_higher_order_node()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAllSatisfied": { "spec": "missing" }, "name": "all positive" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<IEnumerable<int>>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.UnknownSpec);
    }

    [Fact]
    public void Should_load_a_decorated_named_n_satisfied_node_like_its_fluent_equivalent()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "asNSatisfied": { "spec": "is-positive" },
                "n": 2,
                "whenTrue": "exactly two positive",
                "whenFalse": "not exactly two positive",
                "name": "two positive"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .AsNSatisfied(2)
            .WhenTrue("exactly two positive")
            .WhenFalse("not exactly two positive")
            .Create("two positive");

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, 2, -3 }, new[] { -1, -2 });
    }

    [Fact]
    public void Should_load_a_decorated_unnamed_n_satisfied_node_like_its_fluent_equivalent()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "asNSatisfied": { "spec": "is-positive" },
                "n": 2,
                "whenTrue": "exactly two positive",
                "whenFalse": "not exactly two positive"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .AsNSatisfied(2)
            .WhenTrue("exactly two positive")
            .WhenFalse("not exactly two positive")
            .Create();

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, 2, -3 }, new[] { -1, -2 });
    }

    [Fact]
    public void Should_load_a_decorated_named_at_least_n_satisfied_node_like_its_fluent_equivalent()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "asAtLeastNSatisfied": { "spec": "is-positive" },
                "n": 2,
                "whenTrue": "quota met",
                "whenFalse": "quota not met",
                "name": "quota"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .AsAtLeastNSatisfied(2)
            .WhenTrue("quota met")
            .WhenFalse("quota not met")
            .Create("quota");

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, 2, -3 }, new[] { 1, -2, -3 });
    }

    [Fact]
    public void Should_load_a_decorated_unnamed_at_least_n_satisfied_node_like_its_fluent_equivalent()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "asAtLeastNSatisfied": { "spec": "is-positive" },
                "n": 2,
                "whenTrue": "quota met",
                "whenFalse": "quota not met"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .AsAtLeastNSatisfied(2)
            .WhenTrue("quota met")
            .WhenFalse("quota not met")
            .Create();

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, 2, -3 }, new[] { 1, -2, -3 });
    }

    [Fact]
    public void Should_load_a_decorated_named_at_most_n_satisfied_node_like_its_fluent_equivalent()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "asAtMostNSatisfied": { "spec": "is-positive" },
                "n": 1,
                "whenTrue": "within bounds",
                "whenFalse": "over bounds",
                "name": "bounded"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .AsAtMostNSatisfied(1)
            .WhenTrue("within bounds")
            .WhenFalse("over bounds")
            .Create("bounded");

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, -2 }, new[] { 1, 2 });
    }

    [Fact]
    public void Should_load_a_decorated_unnamed_at_most_n_satisfied_node_like_its_fluent_equivalent()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "asAtMostNSatisfied": { "spec": "is-positive" },
                "n": 1,
                "whenTrue": "within bounds",
                "whenFalse": "over bounds"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .AsAtMostNSatisfied(1)
            .WhenTrue("within bounds")
            .WhenFalse("over bounds")
            .Create();

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, -2 }, new[] { 1, 2 });
    }

    private class Customer
    {
        public List<int> Orders { get; set; } = [];

        public Account Account { get; set; } = new();

        public int Age { get; set; }
    }

    private class Account
    {
        public List<int> Orders { get; set; } = [];
    }
}
