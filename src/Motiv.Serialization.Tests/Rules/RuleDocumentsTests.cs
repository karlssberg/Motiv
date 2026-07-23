namespace Motiv.Serialization.Tests.Rules;

public class RuleDocumentsTests
{
    [Fact]
    public void Should_wrap_raw_json()
    {
        // Arrange & Act
        var source = RuleDocuments.FromJson("""{ "rule": { "spec": "x" } }""");

        // Assert
        source.Json.ShouldContain("\"spec\"");
    }

    [Fact]
    public void Should_reject_empty_json()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => RuleDocuments.FromJson("  "));
    }

    [Fact]
    public void Should_read_an_embedded_resource_from_the_calling_assembly()
    {
        // Arrange & Act — resource name matching is by trailing segment, so the
        // project-relative path works without the assembly-name prefix
        var source = RuleDocuments.Embedded("test-rule.json");

        // Assert
        source.Json.ShouldContain("is-active");
    }

    [Fact]
    public void Should_read_an_embedded_resource_by_folder_qualified_name()
    {
        // Arrange & Act — path separators normalize to resource-name dots
        var source = RuleDocuments.Embedded("Rules/test-rule.json");

        // Assert
        source.Json.ShouldContain("is-active");
    }

    [Fact]
    public void Should_throw_a_helpful_error_for_a_missing_resource()
    {
        // Act & Assert
        Should.Throw<InvalidOperationException>(() => RuleDocuments.Embedded("nope.json"))
            .Message.ShouldContain("nope.json");
    }

    [Fact]
    public void Should_match_whole_trailing_segments_only()
    {
        // Arrange — "extra-test-rule.json" is also embedded; a raw EndsWith would treat it
        // as a second match for "test-rule.json" (or, worse, bind it when it is the only
        // resource). Segment-boundary matching must resolve the exact resource uniquely.

        // Act
        var exact = RuleDocuments.Embedded("test-rule.json");
        var nearMiss = RuleDocuments.Embedded("extra-test-rule.json");

        // Assert
        exact.Json.ShouldContain("is-active");
        exact.Json.ShouldNotContain("extra");
        nearMiss.Json.ShouldContain("extra");
    }

    [Fact]
    public void Should_throw_a_helpful_error_for_multiple_matching_resources()
    {
        // Arrange — "shared-rule.json" is embedded under both Rules/dup and Rules/other

        // Act & Assert
        var message = Should.Throw<InvalidOperationException>(() => RuleDocuments.Embedded("shared-rule.json")).Message;
        message.ShouldContain("shared-rule.json");
        message.ShouldContain("Multiple");
    }

    [Fact]
    public void Should_reject_an_empty_resource_name()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => RuleDocuments.Embedded("  "));
    }
}
