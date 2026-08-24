namespace Motiv.Serialization.Tests.Decisions;

/// <summary>
/// The <c>audited</c> flag on the rule document — the opt-in that makes a rule's evaluations
/// recordable. It lives on the document rather than in host configuration so that it is versioned
/// with the rule, governed like any other document change, and cannot be set on a rule that has no
/// stored document to hold it.
/// </summary>
public class AuditedDocumentTests
{
    private static RuleDocument Parse(string json)
    {
        var errors = new List<RuleError>();
        var document = new RuleDocumentParser(new RuleSerializerOptions()).Parse(json, errors);
        errors.ShouldBeEmpty();
        return document.ShouldNotBeNull();
    }

    private static IReadOnlyList<RuleError> Validate(string json) =>
        new RuleSerializer(new SpecRegistry()).Validate(json);

    [Fact]
    public void Should_read_an_audited_document_as_audited()
    {
        // Act
        var document = Parse("""{ "audited": true, "rule": { "spec": "a" } }""");

        // Assert
        document.Audited.ShouldBeTrue();
    }

    [Fact]
    public void Should_read_an_explicitly_unaudited_document_as_not_audited()
    {
        // Act
        var document = Parse("""{ "audited": false, "rule": { "spec": "a" } }""");

        // Assert
        document.Audited.ShouldBeFalse();
    }

    [Fact]
    public void Should_default_to_not_audited_when_the_flag_is_absent()
    {
        // Act
        var document = Parse("""{ "rule": { "spec": "a" } }""");

        // Assert
        document.Audited.ShouldBeFalse();
    }

    [Theory]
    [InlineData("""{ "audited": "true", "rule": { "spec": "a" } }""")]
    [InlineData("""{ "audited": 1, "rule": { "spec": "a" } }""")]
    [InlineData("""{ "audited": null, "rule": { "spec": "a" } }""")]
    public void Should_reject_a_non_boolean_audited_flag(string json)
    {
        // Act
        var errors = Validate(json);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.audited");
    }

    [Fact]
    public void Should_not_report_audited_as_an_unknown_property()
    {
        // Act
        var errors = Validate("""{ "audited": true, "rule": { "spec": "a" } }""");

        // Assert
        errors.ShouldBeEmpty();
    }
}
