namespace Motiv.Serialization.Tests;

public class RuleSemanticValidateTests
{
    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0)
            .WhenTrue("is positive")
            .WhenFalse("is not positive")
            .Create();

    private static SpecBase<int, RuleMetadataTests.RuleOutcome> HasOutcome { get; } =
        Spec.Build((int n) => n > 0)
            .WhenTrue(new RuleMetadataTests.RuleOutcome { Code = "POS" })
            .WhenFalse(new RuleMetadataTests.RuleOutcome { Code = "NEG" })
            .Create("has outcome");

    private static RuleSerializer CreateSerializer() =>
        new(new SpecRegistry()
            .Register("is-positive", IsPositive)
            .Register("has-outcome", HasOutcome));

    [Fact]
    public void Should_return_no_errors_for_a_semantically_valid_document()
    {
        // Act
        var errors = CreateSerializer().Validate<int>("""{ "rule": { "spec": "is-positive" } }""");

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Should_collect_every_unknown_spec_instead_of_throwing()
    {
        // Arrange
        const string json =
            """{ "rule": { "and": [ { "spec": "nope-1" }, { "spec": "nope-2" } ] } }""";

        // Act
        var errors = CreateSerializer().Validate<int>(json);

        // Assert
        errors.Count.ShouldBe(2);
        errors.ShouldAllBe(error => error.Code == RuleErrorCode.UnknownSpec);
        errors.Select(error => error.Path)
            .ShouldBe(["$.rule.and[0]", "$.rule.and[1]"]);
    }

    [Fact]
    public void Should_not_report_unsupplied_required_parameters()
    {
        // Arrange — parameter supply is a Deserialize concern; placeholders stand in here
        const string json =
            """
            {
              "parameters": { "minAge": { "type": "integer" } },
              "rule": { "spec": "is-positive", "whenTrue": "at least {minAge}", "whenFalse": "no" }
            }
            """;

        // Act
        var errors = CreateSerializer().Validate<int>(json);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Should_report_metadata_mismatches_in_a_metadata_validation()
    {
        // Act
        var errors = CreateSerializer()
            .Validate<int, RuleMetadataTests.RuleOutcome>("""{ "rule": { "spec": "is-positive" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.MetadataTypeMismatch);
        error.Path.ShouldBe("$.rule");
    }

    [Fact]
    public void Should_treat_a_string_metadata_validation_as_an_explanation_validation()
    {
        // Act
        var errors = CreateSerializer()
            .Validate<int, string>("""{ "rule": { "spec": "is-positive", "whenTrue": "ok", "whenFalse": "bad" } }""");

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Should_not_bind_when_the_document_has_structural_errors()
    {
        // Arrange — a malformed sibling node must suppress registry lookups entirely
        const string json =
            """{ "rule": { "and": [ { "spec": "unknown-name" }, { "frobnicate": true } ] } }""";

        // Act
        var errors = CreateSerializer().Validate<int>(json);

        // Assert
        errors.ShouldAllBe(error => error.Code == RuleErrorCode.InvalidNode);
    }

    [Fact]
    public void Should_not_crash_when_the_document_is_not_valid_json_at_all()
    {
        // Act
        var errors = CreateSerializer().Validate<int>("not json at all");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$");
    }

    [Fact]
    public void Should_not_bind_when_envelope_errors_accompany_an_otherwise_valid_rule()
    {
        // Arrange — the envelope error alone must suppress the registry lookup that would
        // otherwise also report the unregistered spec name
        const string json = """{ "frobnicate": 1, "rule": { "spec": "totally-unregistered" } }""";

        // Act
        var errors = CreateSerializer().Validate<int>(json);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.frobnicate");
    }

    [Fact]
    public void Should_not_crash_when_the_document_is_not_valid_json_at_all_for_a_metadata_validation()
    {
        // Act
        var errors = CreateSerializer().Validate<int, RuleMetadataTests.RuleOutcome>("not json at all");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$");
    }

    [Fact]
    public void Should_not_bind_when_envelope_errors_accompany_an_otherwise_valid_rule_for_a_metadata_validation()
    {
        // Arrange
        const string json = """{ "frobnicate": 1, "rule": { "spec": "totally-unregistered" } }""";

        // Act
        var errors = CreateSerializer().Validate<int, RuleMetadataTests.RuleOutcome>(json);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.frobnicate");
    }
}
