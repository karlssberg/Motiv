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
                .Register("has-outcome", HasOutcome),
            options);

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
            """{ "rule": { "asAllSatisfied": { "spec": "has-outcome" }, "name": "all coded" } }""";
        var expected = Spec
            .Build(HasOutcome)
            .AsAllSatisfied()
            .Create("all coded");

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, 2 }, new[] { 1, -2 });
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
            .Create("any positive");

        // Act
        var loaded = CreateSerializer().Deserialize<IEnumerable<int>, RuleOutcome>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, new[] { 1, -2 }, new[] { -1, -2 });
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
}
