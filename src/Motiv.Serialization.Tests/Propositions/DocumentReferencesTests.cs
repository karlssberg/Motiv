using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class DocumentReferencesTests
{
    private static IReadOnlyList<string> ReferencesOf(string json)
    {
        var errors = new List<RuleError>();
        var document = new RuleDocumentParser(new RuleSerializerOptions()).Parse(json, errors);
        errors.ShouldBeEmpty();
        return DocumentReferences.From(document!);
    }

    [Fact]
    public void Should_find_the_reference_in_a_single_leaf()
    {
        // Act
        var references = ReferencesOf("""{ "rule": { "spec": "is-active" } }""");

        // Assert
        references.ShouldBe(["is-active"]);
    }

    [Fact]
    public void Should_find_references_across_a_composition()
    {
        // Act
        var references = ReferencesOf(
            """{ "rule": { "and": [ { "spec": "customer.is-active" }, { "spec": "customer.is-adult" } ] } }""");

        // Assert
        references.ShouldBe(["customer.is-active", "customer.is-adult"]);
    }

    [Fact]
    public void Should_find_references_beneath_a_negation()
    {
        // Act
        var references = ReferencesOf("""{ "rule": { "not": { "spec": "is-active" } } }""");

        // Assert
        references.ShouldBe(["is-active"]);
    }

    [Fact]
    public void Should_find_the_reference_inside_a_higher_order_subtree()
    {
        // Act — the quantified child is a real edge: editing is-large-order changes this document's meaning
        var references = ReferencesOf(
            """{ "rule": { "asAllSatisfied": { "spec": "is-large-order" }, "path": "orders" } }""");

        // Assert
        references.ShouldBe(["is-large-order"]);
    }

    [Fact]
    public void Should_report_each_name_once_even_when_referenced_twice()
    {
        // Arrange — the graph needs a set of edges, not a bag
        var json = """
            { "rule": { "or": [ { "spec": "is-active" }, { "and": [ { "spec": "is-active" }, { "spec": "is-adult" } ] } ] } }
            """;

        // Act
        var references = ReferencesOf(json);

        // Assert
        references.ShouldBe(["is-active", "is-adult"]);
    }

    [Fact]
    public void Should_report_no_references_for_a_document_with_no_spec_leaves()
    {
        // Act
        var references = ReferencesOf("""{ "rule": { "expression": "n > 0" } }""");

        // Assert
        references.ShouldBeEmpty();
    }
}
