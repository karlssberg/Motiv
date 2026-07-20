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
        new(new SpecRegistry()
            .Register("is-positive", IsPositive)
            .RegisterCollection<Customer, int>("orders", c => c.Orders)
            .RegisterCollection<Customer, int>("account-orders", c => c.Account.Orders));

    [Fact]
    public void Should_load_a_named_all_satisfied_node_like_its_fluent_equivalent()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAllSatisfied": { "spec": "is-positive" }, "path": "orders", "name": "all positive" } }""";
        var inner = Spec.Build(IsPositive).AsAllSatisfied()
            .WhenTrue("all satisfied").WhenFalse("not all satisfied").Create()
            .ChangeModelTo<Customer>(c => c.Orders);
        var expected = Spec.Build(inner).Create("all positive");

        // Act
        var loaded = CreateSerializer().Deserialize<Customer>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected,
            new Customer { Orders = [1, 2] }, new Customer { Orders = [1, -2] }, new Customer { Orders = [-1, -2] });
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
                "path": "orders",
                "whenTrue": "some are positive",
                "whenFalse": "none are positive"
              }
            }
            """;
        var inner = Spec.Build(IsPositive).AsAnySatisfied()
            .WhenTrue("any satisfied").WhenFalse("none satisfied").Create()
            .ChangeModelTo<Customer>(c => c.Orders);
        var expected = Spec.Build(inner).WhenTrue("some are positive").WhenFalse("none are positive").Create();

        // Act
        var loaded = CreateSerializer().Deserialize<Customer>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new Customer { Orders = [1, -2] }, new Customer { Orders = [-1, -2] });
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
                "path": "orders",
                "whenTrue": "all positive",
                "whenFalse": "some are not positive",
                "name": "positivity"
              }
            }
            """;
        var inner = Spec.Build(IsPositive).AsAllSatisfied()
            .WhenTrue("all satisfied").WhenFalse("not all satisfied").Create()
            .ChangeModelTo<Customer>(c => c.Orders);
        var expected = Spec.Build(inner).WhenTrue("all positive").WhenFalse("some are not positive").Create("positivity");

        // Act
        var loaded = CreateSerializer().Deserialize<Customer>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new Customer { Orders = [1, 2] }, new Customer { Orders = [1, -2] });
    }

    [Fact]
    public void Should_load_an_exactly_n_satisfied_node_with_a_literal_n()
    {
        // Arrange
        const string json =
            """{ "rule": { "asNSatisfied": { "spec": "is-positive" }, "n": 2, "path": "orders", "name": "exactly two" } }""";
        var inner = Spec.Build(IsPositive).AsNSatisfied(2)
            .WhenTrue("exactly 2 satisfied").WhenFalse("not exactly 2 satisfied").Create()
            .ChangeModelTo<Customer>(c => c.Orders);
        var expected = Spec.Build(inner).Create("exactly two");

        // Act
        var loaded = CreateSerializer().Deserialize<Customer>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected,
            new Customer { Orders = [1, 2, -3] }, new Customer { Orders = [1, 2, 3] }, new Customer { Orders = [-1, -2] });
    }

    [Fact]
    public void Should_resolve_n_from_a_parameter_reference()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": { "minOrders": { "type": "integer" } },
              "rule": {
                "asAtLeastNSatisfied": { "spec": "is-positive" }, "n": "@minOrders", "path": "orders", "name": "quota"
              }
            }
            """;
        var inner = Spec.Build(IsPositive).AsAtLeastNSatisfied(2)
            .WhenTrue("at least 2 satisfied").WhenFalse("fewer than 2 satisfied").Create()
            .ChangeModelTo<Customer>(c => c.Orders);
        var expected = Spec.Build(inner).Create("quota");

        // Act
        var loaded = CreateSerializer().Deserialize<Customer>(json, new { minOrders = 2 });

        // Assert
        ShouldBehaveIdentically(loaded, expected, new Customer { Orders = [1, 2, -3] }, new Customer { Orders = [1, -2, -3] });
    }

    [Fact]
    public void Should_load_an_at_most_n_satisfied_node()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAtMostNSatisfied": { "spec": "is-positive" }, "n": 1, "path": "orders", "name": "at most one" } }""";
        var inner = Spec.Build(IsPositive).AsAtMostNSatisfied(1)
            .WhenTrue("at most 1 satisfied").WhenFalse("more than 1 satisfied").Create()
            .ChangeModelTo<Customer>(c => c.Orders);
        var expected = Spec.Build(inner).Create("at most one");

        // Act
        var loaded = CreateSerializer().Deserialize<Customer>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new Customer { Orders = [1, -2] }, new Customer { Orders = [1, 2] });
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
                "path": "orders",
                "name": "contradiction everywhere"
              }
            }
            """;
        var inner = Spec.Build(IsPositive.And(IsPositive.Not())).AsAllSatisfied()
            .WhenTrue("all satisfied").WhenFalse("not all satisfied").Create()
            .ChangeModelTo<Customer>(c => c.Orders);
        var expected = Spec.Build(inner).Create("contradiction everywhere");

        // Act
        var loaded = CreateSerializer().Deserialize<Customer>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new Customer { Orders = [1, 2] }, new Customer { Orders = [-1] });
    }

    [Fact]
    public void Should_report_an_unregistered_collection_path()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAllSatisfied": { "spec": "is-positive" }, "path": "missing", "name": "all positive" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<Customer>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.UnknownCollection);
        error.Path.ShouldBe("$.rule");
    }

    [Fact]
    public void Should_report_an_unknown_parameter_reference_in_n()
    {
        // Arrange
        const string json =
            """{ "rule": { "asNSatisfied": { "spec": "is-positive" }, "n": "@missing", "path": "orders", "name": "x" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<Customer>(json);

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
              "rule": { "asNSatisfied": { "spec": "is-positive" }, "n": "@label", "path": "orders", "name": "x" }
            }
            """;

        // Act
        var act = () => CreateSerializer().Deserialize<Customer>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.ParameterTypeMismatch);
        error.Path.ShouldBe("$.rule.n");
    }

    [Fact]
    public void Should_select_a_registered_collection_and_reanchor_to_the_parent_model()
    {
        // Arrange: the selector can be an arbitrary C# expression (here reaching through a nested
        // Account property) since paths are host-registered rather than reflected from the string.
        const string json =
            """
            {
              "rule": {
                "asAnySatisfied": { "spec": "is-positive" },
                "path": "account-orders",
                "name": "an account order is positive"
              }
            }
            """;
        var inner = Spec.Build(IsPositive).AsAnySatisfied()
            .WhenTrue("any satisfied").WhenFalse("none satisfied").Create()
            .ChangeModelTo<Customer>(c => c.Account.Orders);
        var expected = Spec.Build(inner).Create("an account order is positive");

        // Act
        var loaded = CreateSerializer().Deserialize<Customer>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected,
            new Customer { Account = new Account { Orders = [-1, 2] } },
            new Customer { Account = new Account { Orders = [-1, -2] } });
    }

    [Fact]
    public void Should_propagate_a_failed_inner_spec_through_a_bare_higher_order_node()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAllSatisfied": { "spec": "missing" }, "path": "orders", "name": "all positive" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<Customer>(json);

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
                "path": "orders",
                "whenTrue": "exactly two positive",
                "whenFalse": "not exactly two positive",
                "name": "two positive"
              }
            }
            """;
        var inner = Spec.Build(IsPositive).AsNSatisfied(2)
            .WhenTrue("exactly 2 satisfied").WhenFalse("not exactly 2 satisfied").Create()
            .ChangeModelTo<Customer>(c => c.Orders);
        var expected = Spec.Build(inner)
            .WhenTrue("exactly two positive").WhenFalse("not exactly two positive").Create("two positive");

        // Act
        var loaded = CreateSerializer().Deserialize<Customer>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new Customer { Orders = [1, 2, -3] }, new Customer { Orders = [-1, -2] });
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
                "path": "orders",
                "whenTrue": "exactly two positive",
                "whenFalse": "not exactly two positive"
              }
            }
            """;
        var inner = Spec.Build(IsPositive).AsNSatisfied(2)
            .WhenTrue("exactly 2 satisfied").WhenFalse("not exactly 2 satisfied").Create()
            .ChangeModelTo<Customer>(c => c.Orders);
        var expected = Spec.Build(inner)
            .WhenTrue("exactly two positive").WhenFalse("not exactly two positive").Create();

        // Act
        var loaded = CreateSerializer().Deserialize<Customer>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new Customer { Orders = [1, 2, -3] }, new Customer { Orders = [-1, -2] });
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
                "path": "orders",
                "whenTrue": "quota met",
                "whenFalse": "quota not met",
                "name": "quota"
              }
            }
            """;
        var inner = Spec.Build(IsPositive).AsAtLeastNSatisfied(2)
            .WhenTrue("at least 2 satisfied").WhenFalse("fewer than 2 satisfied").Create()
            .ChangeModelTo<Customer>(c => c.Orders);
        var expected = Spec.Build(inner).WhenTrue("quota met").WhenFalse("quota not met").Create("quota");

        // Act
        var loaded = CreateSerializer().Deserialize<Customer>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new Customer { Orders = [1, 2, -3] }, new Customer { Orders = [1, -2, -3] });
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
                "path": "orders",
                "whenTrue": "quota met",
                "whenFalse": "quota not met"
              }
            }
            """;
        var inner = Spec.Build(IsPositive).AsAtLeastNSatisfied(2)
            .WhenTrue("at least 2 satisfied").WhenFalse("fewer than 2 satisfied").Create()
            .ChangeModelTo<Customer>(c => c.Orders);
        var expected = Spec.Build(inner).WhenTrue("quota met").WhenFalse("quota not met").Create();

        // Act
        var loaded = CreateSerializer().Deserialize<Customer>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new Customer { Orders = [1, 2, -3] }, new Customer { Orders = [1, -2, -3] });
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
                "path": "orders",
                "whenTrue": "within bounds",
                "whenFalse": "over bounds",
                "name": "bounded"
              }
            }
            """;
        var inner = Spec.Build(IsPositive).AsAtMostNSatisfied(1)
            .WhenTrue("at most 1 satisfied").WhenFalse("more than 1 satisfied").Create()
            .ChangeModelTo<Customer>(c => c.Orders);
        var expected = Spec.Build(inner).WhenTrue("within bounds").WhenFalse("over bounds").Create("bounded");

        // Act
        var loaded = CreateSerializer().Deserialize<Customer>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new Customer { Orders = [1, -2] }, new Customer { Orders = [1, 2] });
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
                "path": "orders",
                "whenTrue": "within bounds",
                "whenFalse": "over bounds"
              }
            }
            """;
        var inner = Spec.Build(IsPositive).AsAtMostNSatisfied(1)
            .WhenTrue("at most 1 satisfied").WhenFalse("more than 1 satisfied").Create()
            .ChangeModelTo<Customer>(c => c.Orders);
        var expected = Spec.Build(inner).WhenTrue("within bounds").WhenFalse("over bounds").Create();

        // Act
        var loaded = CreateSerializer().Deserialize<Customer>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new Customer { Orders = [1, -2] }, new Customer { Orders = [1, 2] });
    }

    private sealed class Customer
    {
        public List<int> Orders { get; set; } = [];

        public Account Account { get; set; } = new();

        public int Age { get; set; }
    }

    private sealed class Account
    {
        public List<int> Orders { get; set; } = [];
    }
}
