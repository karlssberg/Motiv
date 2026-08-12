namespace Motiv.Serialization.Tests.Governance;

public class RuleDocumentComparerTests
{
    private static RuleDocument AParsedDocument(string json)
    {
        var errors = new List<RuleError>();
        var document = new RuleDocumentParser(new RuleSerializerOptions()).Parse(json, errors);
        errors.ShouldBeEmpty();
        return document!;
    }

    [Fact]
    public void Should_treat_identical_documents_as_structurally_equal()
    {
        // Arrange
        const string json =
            """{ "rule": { "and": [ { "spec": "is-active" }, { "spec": "is-adult" } ] } }""";
        var left = AParsedDocument(json);
        var right = AParsedDocument(json);

        // Act
        var equal = RuleDocumentComparer.StructurallyEqual(left, right);

        // Assert
        equal.ShouldBeTrue();
    }

    [Fact]
    public void Should_treat_a_whenTrue_text_only_change_as_structurally_equal()
    {
        // Arrange — the metadata-only case: same tree, different display text
        var left = AParsedDocument(
            """
            {
              "rule": {
                "and": [ { "spec": "is-active" }, { "spec": "is-adult" } ],
                "whenTrue": "eligible",
                "whenFalse": "not eligible"
              }
            }
            """);
        var right = AParsedDocument(
            """
            {
              "rule": {
                "and": [ { "spec": "is-active" }, { "spec": "is-adult" } ],
                "whenTrue": "OK",
                "whenFalse": "not OK"
              }
            }
            """);

        // Act
        var equal = RuleDocumentComparer.StructurallyEqual(left, right);

        // Assert
        equal.ShouldBeTrue();
    }

    [Fact]
    public void Should_treat_a_swapped_operator_as_structurally_unequal()
    {
        // Arrange
        var left = AParsedDocument(
            """{ "rule": { "and": [ { "spec": "is-active" }, { "spec": "is-adult" } ] } }""");
        var right = AParsedDocument(
            """{ "rule": { "or": [ { "spec": "is-active" }, { "spec": "is-adult" } ] } }""");

        // Act
        var equal = RuleDocumentComparer.StructurallyEqual(left, right);

        // Assert
        equal.ShouldBeFalse();
    }

    [Fact]
    public void Should_treat_an_added_child_as_structurally_unequal()
    {
        // Arrange
        var left = AParsedDocument(
            """{ "rule": { "and": [ { "spec": "is-active" }, { "spec": "is-adult" } ] } }""");
        var right = AParsedDocument(
            """
            {
              "rule": {
                "and": [ { "spec": "is-active" }, { "spec": "is-adult" }, { "spec": "is-verified" } ]
              }
            }
            """);

        // Act
        var equal = RuleDocumentComparer.StructurallyEqual(left, right);

        // Assert
        equal.ShouldBeFalse();
    }

    [Fact]
    public void Should_treat_a_changed_spec_name_as_structurally_unequal()
    {
        // Arrange
        var left = AParsedDocument(
            """{ "rule": { "and": [ { "spec": "is-active" }, { "spec": "is-adult" } ] } }""");
        var right = AParsedDocument(
            """{ "rule": { "and": [ { "spec": "is-active" }, { "spec": "is-verified" } ] } }""");

        // Act
        var equal = RuleDocumentComparer.StructurallyEqual(left, right);

        // Assert
        equal.ShouldBeFalse();
    }

    [Fact]
    public void Should_treat_a_changed_higher_order_n_as_structurally_unequal()
    {
        // Arrange
        var left = AParsedDocument(
            """
            { "rule": { "asNSatisfied": { "spec": "is-positive" }, "n": 2, "path": "orders" } }
            """);
        var right = AParsedDocument(
            """
            { "rule": { "asNSatisfied": { "spec": "is-positive" }, "n": 3, "path": "orders" } }
            """);

        // Act
        var equal = RuleDocumentComparer.StructurallyEqual(left, right);

        // Assert
        equal.ShouldBeFalse();
    }

    [Fact]
    public void Should_treat_a_changed_parameter_default_as_structurally_unequal()
    {
        // Arrange — same tree, but the declared parameter's default value differs: a logic change
        var left = AParsedDocument(
            """
            {
              "parameters": { "label": { "type": "string", "default": "x" } },
              "rule": { "spec": "is-active" }
            }
            """);
        var right = AParsedDocument(
            """
            {
              "parameters": { "label": { "type": "string", "default": "y" } },
              "rule": { "spec": "is-active" }
            }
            """);

        // Act
        var equal = RuleDocumentComparer.StructurallyEqual(left, right);

        // Assert
        equal.ShouldBeFalse();
    }

    [Fact]
    public void Should_treat_identical_parameter_declarations_as_structurally_equal()
    {
        // Arrange
        const string json =
            """
            {
              "parameters": { "label": { "type": "string", "default": "x" } },
              "rule": { "spec": "is-active" }
            }
            """;
        var left = AParsedDocument(json);
        var right = AParsedDocument(json);

        // Act
        var equal = RuleDocumentComparer.StructurallyEqual(left, right);

        // Assert
        equal.ShouldBeTrue();
    }
}
