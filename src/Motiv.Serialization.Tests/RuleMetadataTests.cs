using System.Text.Json;
using static Motiv.Serialization.Tests.SpecAssertions;

namespace Motiv.Serialization.Tests;

public class RuleMetadataTests
{
    public class RuleOutcome
    {
        public string Code { get; set; } = "";

        public override bool Equals(object? obj) => obj is RuleOutcome other && other.Code == Code;

        public override int GetHashCode() => Code.GetHashCode();

        public override string ToString() => Code;
    }

    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0)
            .WhenTrue("is positive")
            .WhenFalse("is not positive")
            .Create();

    private static SpecBase<int, RuleOutcome> HasOutcome { get; } =
        Spec.Build((int n) => n > 0)
            .WhenTrue(new RuleOutcome { Code = "POS" })
            .WhenFalse(new RuleOutcome { Code = "NEG" })
            .Create("has outcome");

    private static RuleSerializer CreateSerializer(RuleSerializerOptions? options = null) =>
        new(new SpecRegistry()
                .Register("is-positive", IsPositive)
                .Register("has-outcome", HasOutcome)
                .RegisterCollection<Customer, int>("orders", c => c.Orders),
            options);

    private sealed class Customer
    {
        public List<int> Orders { get; set; } = [];
    }

    [Fact]
    public void Should_load_a_typed_registry_leaf_with_its_metadata_intact()
    {
        // Arrange
        const string json = """{ "rule": { "spec": "has-outcome" } }""";

        // Act
        var loaded = CreateSerializer().Deserialize<int, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, HasOutcome, 5, -5);
    }

    [Fact]
    public void Should_remetadatize_a_string_leaf_with_object_payloads()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "spec": "is-positive",
                "whenTrue": { "Code": "OK" },
                "whenFalse": { "Code": "BAD" },
                "name": "coded"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .WhenTrue(new RuleOutcome { Code = "OK" })
            .WhenFalse(new RuleOutcome { Code = "BAD" })
            .Create("coded");

        // Act
        var loaded = CreateSerializer().Deserialize<int, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_honor_the_configured_metadata_json_options()
    {
        // Arrange — camelCase payload properties only bind through MetadataJsonOptions
        const string json =
            """
            {
              "rule": {
                "spec": "is-positive",
                "whenTrue": { "code": "OK" },
                "whenFalse": { "code": "BAD" },
                "name": "coded"
              }
            }
            """;
        var options = new RuleSerializerOptions
        {
            MetadataJsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        };
        var expected = Spec
            .Build(IsPositive)
            .WhenTrue(new RuleOutcome { Code = "OK" })
            .WhenFalse(new RuleOutcome { Code = "BAD" })
            .Create("coded");

        // Act
        var loaded = CreateSerializer(options).Deserialize<int, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_compose_typed_and_remetadatized_nodes()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "and": [
                  { "spec": "has-outcome" },
                  { "spec": "is-positive",
                    "whenTrue": { "Code": "OK" },
                    "whenFalse": { "Code": "BAD" },
                    "name": "coded" }
                ]
              }
            }
            """;
        var expected = HasOutcome.And(
            Spec.Build(IsPositive)
                .WhenTrue(new RuleOutcome { Code = "OK" })
                .WhenFalse(new RuleOutcome { Code = "BAD" })
                .Create("coded"));

        // Act
        var loaded = CreateSerializer().Deserialize<int, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_reject_an_undecorated_string_leaf_in_a_metadata_load()
    {
        // Arrange
        const string json = """{ "rule": { "spec": "is-positive" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int, RuleOutcome>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.MetadataTypeMismatch);
        error.Path.ShouldBe("$.rule");
    }

    [Fact]
    public void Should_reject_string_payloads_in_a_metadata_load()
    {
        // Arrange
        const string json =
            """{ "rule": { "spec": "has-outcome", "whenTrue": "yes", "whenFalse": "no" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int, RuleOutcome>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.MetadataTypeMismatch);
        error.Path.ShouldBe("$.rule");
    }

    [Fact]
    public void Should_report_a_payload_that_cannot_deserialize_to_the_metadata_type()
    {
        // Arrange — Code is a string; an object payload for it fails STJ deserialization
        const string json =
            """
            {
              "rule": {
                "spec": "is-positive",
                "whenTrue": { "Code": { "nested": true } },
                "whenFalse": { "Code": "BAD" },
                "name": "coded"
              }
            }
            """;

        // Act
        var act = () => CreateSerializer().Deserialize<int, RuleOutcome>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.MetadataTypeMismatch);
        error.Path.ShouldBe("$.rule.whenTrue");
    }

    [Fact]
    public void Should_load_a_bare_higher_order_node_over_typed_inner_metadata()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAllSatisfied": { "spec": "has-outcome" }, "path": "orders", "name": "all coded" } }""";
        var expected = Spec
            .Build(HasOutcome)
            .AsAllSatisfied()
            .Create("all coded")
            .ChangeModelTo<Customer>(c => c.Orders);

        // Act
        var loaded = CreateSerializer().Deserialize<Customer, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new Customer { Orders = [1, 2] }, new Customer { Orders = [1, -2] });
    }

    [Fact]
    public void Should_remetadatize_a_higher_order_node_with_object_payloads()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "asAnySatisfied": { "spec": "is-positive" },
                "path": "orders",
                "whenTrue": { "Code": "SOME" },
                "whenFalse": { "Code": "NONE" },
                "name": "any positive"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .AsAnySatisfied()
            .WhenTrue(new RuleOutcome { Code = "SOME" })
            .WhenFalse(new RuleOutcome { Code = "NONE" })
            .Create("any positive")
            .ChangeModelTo<Customer>(c => c.Orders);

        // Act
        var loaded = CreateSerializer().Deserialize<Customer, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new Customer { Orders = [1, -2] }, new Customer { Orders = [-1, -2] });
    }

    [Fact]
    public void Should_reject_a_bare_higher_order_node_with_no_name_or_payload_in_a_metadata_load()
    {
        // Arrange: unlike the string/explanation loader, a metadata load cannot synthesize a
        // default WhenTrue/WhenFalse for an arbitrary metadata type, so this must fail cleanly
        // rather than crash.
        const string json = """{ "rule": { "asAllSatisfied": { "spec": "has-outcome" }, "path": "orders" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<Customer, RuleOutcome>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.MetadataTypeMismatch);
        error.Path.ShouldBe("$.rule");
    }

    [Fact]
    public void Should_reject_object_payloads_nested_inside_a_remetadatized_subtree()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "and": [
                  { "spec": "is-positive" },
                  { "spec": "is-positive",
                    "whenTrue": { "Code": "A" },
                    "whenFalse": { "Code": "B" },
                    "name": "inner" }
                ],
                "whenTrue": { "Code": "OUTER-T" },
                "whenFalse": { "Code": "OUTER-F" },
                "name": "outer"
              }
            }
            """;

        // Act
        var act = () => CreateSerializer().Deserialize<int, RuleOutcome>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        exception.Errors.ShouldContain(error =>
            error.Code == RuleErrorCode.MetadataTypeMismatch && error.Path == "$.rule.and[1]");
    }

    [Fact]
    public void Should_treat_a_string_metadata_load_as_an_explanation_load()
    {
        // Arrange
        const string json =
            """{ "rule": { "spec": "is-positive", "whenTrue": "ok", "whenFalse": "bad" } }""";
        var expected = Spec
            .Build(IsPositive)
            .WhenTrue("ok")
            .WhenFalse("bad")
            .Create();

        // Act
        var loaded = CreateSerializer().Deserialize<int, string>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_wrap_the_root_with_the_document_name_in_a_metadata_load()
    {
        // Arrange
        const string json = """{ "name": "document rule", "rule": { "spec": "has-outcome" } }""";
        var expected = Spec.Build(HasOutcome).Create("document rule");

        // Act
        var loaded = CreateSerializer().Deserialize<int, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_bind_a_not_node_to_the_fluent_negation_in_a_metadata_load()
    {
        // Arrange
        const string json = """{ "rule": { "not": { "spec": "has-outcome" } } }""";

        // Act
        var loaded = CreateSerializer().Deserialize<int, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, HasOutcome.Not(), 5, -5);
    }

    [Fact]
    public void Should_propagate_a_failed_child_through_a_metadata_not_node()
    {
        // Arrange
        const string json = """{ "rule": { "not": { "spec": "missing" } } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int, RuleOutcome>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.UnknownSpec);
    }

    [Fact]
    public void Should_throw_for_an_expression_node_in_a_metadata_load_when_expressions_are_not_enabled()
    {
        // Arrange
        const string json = """{ "rule": { "expression": "Age >= 18" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int, RuleOutcome>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.ExpressionsNotEnabled);
    }

    [Fact]
    public void Should_load_a_name_only_metadata_node_like_a_fluent_create_wrapper()
    {
        // Arrange
        const string json = """{ "rule": { "spec": "has-outcome", "name": "wrapper" } }""";
        var expected = Spec.Build(HasOutcome).Create("wrapper");

        // Act
        var loaded = CreateSerializer().Deserialize<int, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_throw_for_an_unknown_spec_name_in_a_metadata_load()
    {
        // Arrange
        const string json = """{ "rule": { "spec": "missing" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int, RuleOutcome>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.UnknownSpec);
        error.Path.ShouldBe("$.rule");
    }

    [Fact]
    public void Should_throw_when_a_sync_load_references_an_async_spec_in_a_metadata_load()
    {
        // Arrange
        var isReadyAsync = Spec
            .BuildAsync((int n) => new ValueTask<bool>(n > 0))
            .Create("is ready");
        var registry = new SpecRegistry().Register("is-ready", isReadyAsync);
        var serializer = new RuleSerializer(registry);
        const string json = """{ "rule": { "spec": "is-ready" } }""";

        // Act
        var act = () => serializer.Deserialize<int, RuleOutcome>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.AsyncSpecInSyncLoad);
        error.Message.ShouldContain("is-ready");
    }

    [Fact]
    public void Should_throw_for_a_model_type_mismatch_in_a_metadata_load()
    {
        // Arrange
        const string json = """{ "rule": { "spec": "has-outcome" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<string, RuleOutcome>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.ModelTypeMismatch);
        error.Message.ShouldContain("Int32");
        error.Message.ShouldContain("String");
    }

    [Fact]
    public void Should_propagate_a_failed_child_through_a_metadata_composition()
    {
        // Arrange
        const string json =
            """{ "rule": { "and": [ { "spec": "has-outcome" }, { "spec": "missing" } ] } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int, RuleOutcome>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.UnknownSpec);
    }

    private static SpecBase<int, RuleOutcome> IsEvenOutcome { get; } =
        Spec.Build((int n) => n % 2 == 0)
            .WhenTrue(new RuleOutcome { Code = "EVEN" })
            .WhenFalse(new RuleOutcome { Code = "ODD" })
            .Create("is even outcome");

    private static RuleSerializer CreateSerializerWithBothOutcomes() =>
        new(new SpecRegistry()
            .Register("is-positive", IsPositive)
            .Register("has-outcome", HasOutcome)
            .Register("is-even-outcome", IsEvenOutcome));

    public static TheoryData<string, string> MetadataBinaryOperators => new()
    {
        { "and", "And" },
        { "or", "Or" },
        { "xor", "XOr" },
        { "andAlso", "AndAlso" },
        { "orElse", "OrElse" }
    };

    private static SpecBase<int, RuleOutcome> ComposeOutcomes(
        string method,
        SpecBase<int, RuleOutcome> left,
        SpecBase<int, RuleOutcome> right) =>
        method switch
        {
            "And" => left.And(right),
            "Or" => left.Or(right),
            "XOr" => left.XOr(right),
            "AndAlso" => left.AndAlso(right),
            _ => left.OrElse(right)
        };

    [Theory]
    [MemberData(nameof(MetadataBinaryOperators))]
    public void Should_bind_each_binary_operator_to_its_fluent_equivalent_in_a_metadata_load(
        string jsonOperator,
        string method)
    {
        // Arrange
        var json =
            $$"""{ "rule": { "{{jsonOperator}}": [ { "spec": "has-outcome" }, { "spec": "is-even-outcome" } ] } }""";
        var expected = ComposeOutcomes(method, HasOutcome, IsEvenOutcome);

        // Act
        var loaded = CreateSerializerWithBothOutcomes().Deserialize<int, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 2, 3, -2, -3);
    }

    [Fact]
    public void Should_report_a_higher_order_node_with_an_unregistered_collection_path_in_a_metadata_load()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAllSatisfied": { "spec": "has-outcome" }, "path": "missing", "name": "all coded" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<Customer, RuleOutcome>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.UnknownCollection);
    }

    [Fact]
    public void Should_propagate_a_failed_inner_spec_through_a_remetadatized_higher_order_node()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "asAnySatisfied": { "spec": "missing" },
                "path": "orders",
                "whenTrue": { "Code": "SOME" },
                "whenFalse": { "Code": "NONE" },
                "name": "any positive"
              }
            }
            """;

        // Act
        var act = () => CreateSerializer().Deserialize<Customer, RuleOutcome>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.UnknownSpec);
    }

    [Fact]
    public void Should_propagate_a_failed_inner_spec_through_a_bare_higher_order_node()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAllSatisfied": { "spec": "missing" }, "path": "orders", "name": "all coded" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<Customer, RuleOutcome>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.UnknownSpec);
    }

    [Fact]
    public void Should_remetadatize_an_all_satisfied_higher_order_node()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "asAllSatisfied": { "spec": "is-positive" },
                "path": "orders",
                "whenTrue": { "Code": "ALL" },
                "whenFalse": { "Code": "NOT-ALL" },
                "name": "all positive"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .AsAllSatisfied()
            .WhenTrue(new RuleOutcome { Code = "ALL" })
            .WhenFalse(new RuleOutcome { Code = "NOT-ALL" })
            .Create("all positive")
            .ChangeModelTo<Customer>(c => c.Orders);

        // Act
        var loaded = CreateSerializer().Deserialize<Customer, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new Customer { Orders = [1, 2] }, new Customer { Orders = [1, -2] });
    }

    [Fact]
    public void Should_remetadatize_an_n_satisfied_higher_order_node()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "asNSatisfied": { "spec": "is-positive" },
                "n": 2,
                "path": "orders",
                "whenTrue": { "Code": "TWO" },
                "whenFalse": { "Code": "NOT-TWO" },
                "name": "exactly two"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .AsNSatisfied(2)
            .WhenTrue(new RuleOutcome { Code = "TWO" })
            .WhenFalse(new RuleOutcome { Code = "NOT-TWO" })
            .Create("exactly two")
            .ChangeModelTo<Customer>(c => c.Orders);

        // Act
        var loaded = CreateSerializer().Deserialize<Customer, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new Customer { Orders = [1, 2, -3] }, new Customer { Orders = [-1, -2] });
    }

    [Fact]
    public void Should_remetadatize_an_at_least_n_satisfied_higher_order_node()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "asAtLeastNSatisfied": { "spec": "is-positive" },
                "n": 2,
                "path": "orders",
                "whenTrue": { "Code": "QUOTA" },
                "whenFalse": { "Code": "SHORT" },
                "name": "quota"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .AsAtLeastNSatisfied(2)
            .WhenTrue(new RuleOutcome { Code = "QUOTA" })
            .WhenFalse(new RuleOutcome { Code = "SHORT" })
            .Create("quota")
            .ChangeModelTo<Customer>(c => c.Orders);

        // Act
        var loaded = CreateSerializer().Deserialize<Customer, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new Customer { Orders = [1, 2, -3] }, new Customer { Orders = [1, -2, -3] });
    }

    [Fact]
    public void Should_remetadatize_an_at_most_n_satisfied_higher_order_node()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "asAtMostNSatisfied": { "spec": "is-positive" },
                "n": 1,
                "path": "orders",
                "whenTrue": { "Code": "OK" },
                "whenFalse": { "Code": "TOO-MANY" },
                "name": "at most one"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive)
            .AsAtMostNSatisfied(1)
            .WhenTrue(new RuleOutcome { Code = "OK" })
            .WhenFalse(new RuleOutcome { Code = "TOO-MANY" })
            .Create("at most one")
            .ChangeModelTo<Customer>(c => c.Orders);

        // Act
        var loaded = CreateSerializer().Deserialize<Customer, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new Customer { Orders = [1, -2] }, new Customer { Orders = [1, 2] });
    }

    [Fact]
    public void Should_load_a_bare_any_satisfied_node_over_typed_inner_metadata()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAnySatisfied": { "spec": "has-outcome" }, "path": "orders", "name": "any coded" } }""";
        var expected = Spec.Build(HasOutcome).AsAnySatisfied().Create("any coded").ChangeModelTo<Customer>(c => c.Orders);

        // Act
        var loaded = CreateSerializer().Deserialize<Customer, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new Customer { Orders = [1, -2] }, new Customer { Orders = [-1, -2] });
    }

    [Fact]
    public void Should_load_a_bare_n_satisfied_node_over_typed_inner_metadata()
    {
        // Arrange
        const string json =
            """{ "rule": { "asNSatisfied": { "spec": "has-outcome" }, "n": 2, "path": "orders", "name": "two coded" } }""";
        var expected = Spec.Build(HasOutcome).AsNSatisfied(2).Create("two coded").ChangeModelTo<Customer>(c => c.Orders);

        // Act
        var loaded = CreateSerializer().Deserialize<Customer, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new Customer { Orders = [1, 2, -3] }, new Customer { Orders = [-1, -2] });
    }

    [Fact]
    public void Should_load_a_bare_at_least_n_satisfied_node_over_typed_inner_metadata()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAtLeastNSatisfied": { "spec": "has-outcome" }, "n": 2, "path": "orders", "name": "quota coded" } }""";
        var expected = Spec.Build(HasOutcome).AsAtLeastNSatisfied(2).Create("quota coded").ChangeModelTo<Customer>(c => c.Orders);

        // Act
        var loaded = CreateSerializer().Deserialize<Customer, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new Customer { Orders = [1, 2, -3] }, new Customer { Orders = [1, -2, -3] });
    }

    [Fact]
    public void Should_load_a_bare_at_most_n_satisfied_node_over_typed_inner_metadata()
    {
        // Arrange
        const string json =
            """{ "rule": { "asAtMostNSatisfied": { "spec": "has-outcome" }, "n": 1, "path": "orders", "name": "at most coded" } }""";
        var expected = Spec.Build(HasOutcome).AsAtMostNSatisfied(1).Create("at most coded").ChangeModelTo<Customer>(c => c.Orders);

        // Act
        var loaded = CreateSerializer().Deserialize<Customer, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new Customer { Orders = [1, -2] }, new Customer { Orders = [1, 2] });
    }
}
