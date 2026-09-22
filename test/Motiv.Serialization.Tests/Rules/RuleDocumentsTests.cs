using System.Reflection;

namespace Motiv.Serialization.Tests.Rules;

public class RuleDocumentsTests
{
    // Embedded(string) infers its caller from the stack. Inside a lambda that Shouldly invokes,
    // the JIT can fold the lambda into Shouldly's frame, leaving Shouldly as the apparent caller
    // and the lookup pointed at the wrong assembly. The throw-assertions below therefore name the
    // assembly, so what they are testing is the message rather than the JIT. Calls made straight
    // from a test method body are unaffected — xUnit invokes those by reflection — and they are
    // what covers the inferred-caller path.
    private static readonly Assembly TestAssembly = typeof(RuleDocumentsTests).Assembly;

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
        Should.Throw<InvalidOperationException>(() => RuleDocuments.Embedded("nope.json", TestAssembly))
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
        var message = Should
            .Throw<InvalidOperationException>(() => RuleDocuments.Embedded("shared-rule.json", TestAssembly))
            .Message;
        message.ShouldContain("shared-rule.json");
        message.ShouldContain("Multiple");
    }

    [Fact]
    public void Should_reject_an_empty_resource_name()
    {
        // Act & Assert — left inferring its caller, unlike the throw-assertions above: the name
        // is rejected before any assembly is consulted, so which one it resolves to cannot matter.
        Should.Throw<ArgumentException>(() => RuleDocuments.Embedded("  "));
    }
}
