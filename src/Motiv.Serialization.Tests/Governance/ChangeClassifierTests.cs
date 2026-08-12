namespace Motiv.Serialization.Tests.Governance;

public class ChangeClassifierTests
{
    private const string ActiveOnlyRuleJson = """{ "rule": { "spec": "is-active" } }""";

    private const string ActiveOnlyRuleWithTextJson =
        """
        {
          "rule": {
            "spec": "is-active",
            "whenTrue": "eligible",
            "whenFalse": "not eligible"
          }
        }
        """;

    private const string ActiveOnlyRuleWithDifferentTextJson =
        """
        {
          "rule": {
            "spec": "is-active",
            "whenTrue": "OK",
            "whenFalse": "not OK"
          }
        }
        """;

    private const string AndOfActiveAndAdultRuleJson =
        """{ "rule": { "and": [ { "spec": "is-active" }, { "spec": "is-adult" } ] } }""";

    private static bool NoSpecIsAsync(string specName) => false;

    [Fact]
    public void Should_classify_as_creation_when_the_target_does_not_exist()
    {
        // Arrange & Act
        var classification = ChangeClassifier.Classify(
            proposedDocumentJson: ActiveOnlyRuleJson,
            baseDocumentJson: null,
            targetExists: false,
            specIsAsync: NoSpecIsAsync,
            rollbackOfVersion: null);

        // Assert
        classification.IsCreation.ShouldBeTrue();
    }

    [Fact]
    public void Should_not_classify_as_creation_when_the_target_already_exists()
    {
        // Arrange & Act
        var classification = ChangeClassifier.Classify(
            proposedDocumentJson: ActiveOnlyRuleJson,
            baseDocumentJson: ActiveOnlyRuleJson,
            targetExists: true,
            specIsAsync: NoSpecIsAsync,
            rollbackOfVersion: null);

        // Assert
        classification.IsCreation.ShouldBeFalse();
    }

    [Fact]
    public void Should_classify_as_deletion_when_the_target_exists_and_the_proposed_document_is_null()
    {
        // Arrange & Act
        var classification = ChangeClassifier.Classify(
            proposedDocumentJson: null,
            baseDocumentJson: ActiveOnlyRuleJson,
            targetExists: true,
            specIsAsync: NoSpecIsAsync,
            rollbackOfVersion: null);

        // Assert
        classification.IsDeletion.ShouldBeTrue();
    }

    [Fact]
    public void Should_not_classify_as_deletion_when_the_target_does_not_exist()
    {
        // Arrange & Act — a null proposed document with no existing target is not a deletion
        var classification = ChangeClassifier.Classify(
            proposedDocumentJson: null,
            baseDocumentJson: null,
            targetExists: false,
            specIsAsync: NoSpecIsAsync,
            rollbackOfVersion: null);

        // Assert
        classification.IsDeletion.ShouldBeFalse();
    }

    [Fact]
    public void Should_classify_as_metadata_only_when_only_display_text_changes()
    {
        // Arrange & Act
        var classification = ChangeClassifier.Classify(
            proposedDocumentJson: ActiveOnlyRuleWithDifferentTextJson,
            baseDocumentJson: ActiveOnlyRuleWithTextJson,
            targetExists: true,
            specIsAsync: NoSpecIsAsync,
            rollbackOfVersion: null);

        // Assert
        classification.IsMetadataOnly.ShouldBeTrue();
    }

    [Fact]
    public void Should_not_classify_as_metadata_only_when_the_logic_tree_changes()
    {
        // Arrange & Act — different operator/children: a logic change, not metadata-only
        var classification = ChangeClassifier.Classify(
            proposedDocumentJson: AndOfActiveAndAdultRuleJson,
            baseDocumentJson: ActiveOnlyRuleJson,
            targetExists: true,
            specIsAsync: NoSpecIsAsync,
            rollbackOfVersion: null);

        // Assert
        classification.IsMetadataOnly.ShouldBeFalse();
    }

    [Fact]
    public void Should_classify_as_touching_an_async_spec_when_a_referenced_spec_is_async()
    {
        // Arrange
        bool SpecIsAsync(string specName) => specName == "is-adult";

        // Act
        var classification = ChangeClassifier.Classify(
            proposedDocumentJson: AndOfActiveAndAdultRuleJson,
            baseDocumentJson: null,
            targetExists: false,
            specIsAsync: SpecIsAsync,
            rollbackOfVersion: null);

        // Assert
        classification.TouchesAsyncSpec.ShouldBeTrue();
    }

    [Fact]
    public void Should_not_classify_as_touching_an_async_spec_when_no_referenced_spec_is_async()
    {
        // Arrange & Act
        var classification = ChangeClassifier.Classify(
            proposedDocumentJson: AndOfActiveAndAdultRuleJson,
            baseDocumentJson: null,
            targetExists: false,
            specIsAsync: NoSpecIsAsync,
            rollbackOfVersion: null);

        // Assert
        classification.TouchesAsyncSpec.ShouldBeFalse();
    }

    [Fact]
    public void Should_store_rollback_intent_when_a_rollback_version_is_supplied()
    {
        // Arrange & Act
        var classification = ChangeClassifier.Classify(
            proposedDocumentJson: ActiveOnlyRuleJson,
            baseDocumentJson: ActiveOnlyRuleJson,
            targetExists: true,
            specIsAsync: NoSpecIsAsync,
            rollbackOfVersion: 3);

        // Assert
        classification.IsRollback.ShouldBeTrue();
        classification.RollbackOfVersion.ShouldBe(3);
    }

    [Fact]
    public void Should_not_store_rollback_intent_when_no_rollback_version_is_supplied()
    {
        // Arrange & Act
        var classification = ChangeClassifier.Classify(
            proposedDocumentJson: ActiveOnlyRuleJson,
            baseDocumentJson: ActiveOnlyRuleJson,
            targetExists: true,
            specIsAsync: NoSpecIsAsync,
            rollbackOfVersion: null);

        // Assert
        classification.IsRollback.ShouldBeFalse();
    }

    [Fact]
    public void Should_not_classify_as_touching_an_async_spec_when_the_proposed_document_is_null()
    {
        // Arrange & Act
        var classification = ChangeClassifier.Classify(
            proposedDocumentJson: null,
            baseDocumentJson: ActiveOnlyRuleJson,
            targetExists: true,
            specIsAsync: NoSpecIsAsync,
            rollbackOfVersion: null);

        // Assert
        classification.TouchesAsyncSpec.ShouldBeFalse();
    }

    [Fact]
    public void Should_not_classify_as_metadata_only_when_the_proposed_document_is_unparseable()
    {
        // Arrange & Act
        var classification = ChangeClassifier.Classify(
            proposedDocumentJson: "{ not valid json",
            baseDocumentJson: ActiveOnlyRuleJson,
            targetExists: true,
            specIsAsync: NoSpecIsAsync,
            rollbackOfVersion: null);

        // Assert
        classification.IsMetadataOnly.ShouldBeFalse();
    }

    [Fact]
    public void Should_not_classify_as_touching_an_async_spec_when_the_proposed_document_is_unparseable()
    {
        // Arrange & Act
        var classification = ChangeClassifier.Classify(
            proposedDocumentJson: "{ not valid json",
            baseDocumentJson: null,
            targetExists: false,
            specIsAsync: _ => true,
            rollbackOfVersion: null);

        // Assert
        classification.TouchesAsyncSpec.ShouldBeFalse();
    }
}
