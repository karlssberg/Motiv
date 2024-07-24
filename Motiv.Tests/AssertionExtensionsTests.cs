namespace Motiv.Tests;

public class AssertionExtensionsTests
{
    [Theory]
    [AutoData]
    public void Should_gather_metadata_from_a_collection_of_metadata_boolean_results(string[] models)
    {
        // Arrange
        var spec = Spec
            .Build((string _) => true)
            .WhenTrue(m => m)
            .WhenFalse(m => m)
            .Create("is positive");

        var booleanResultsCollection = models.Select(spec.IsSatisfiedBy);

        // Act
        var act = booleanResultsCollection.GetAssertions();

        // Assert
        act.Should().BeEquivalentTo(models);
    }


    [Theory]
    [AutoData]
    public void Should_gather_metadata_associated_with_true_results(string[] models)
    {
        // Arrange
        var spec = Spec
            .Build((string _) => true)
            .WhenTrue(m => m)
            .WhenFalse(m => m)
            .Create("is always true");

        var booleanResultsCollection = models.Select(spec.IsSatisfiedBy);

        // Act
        var act = booleanResultsCollection.GetTrueAssertions();

        // Assert
        act.Should().BeEquivalentTo(models);
    }

    [Theory]
    [AutoData]
    public void Should_gather_metadata_associated_with_false_results(string[] models)
    {
        // Arrange
        var spec = Spec
            .Build((string _) => false)
            .WhenTrue(m => m)
            .WhenFalse(m => m)
            .Create("is always false");

        var booleanResultsCollection = models.Select(spec.IsSatisfiedBy);

        // Act
        var act = booleanResultsCollection.GetFalseAssertions();

        // Assert
        act.Should().BeEquivalentTo(models);
    }

    [Theory]
    [AutoData]
    public void Should_gather_metadata_from_a_collection_of_metadata_nodes(IEnumerable<string> assertions)
    {
        // Arrange
        var metadataNodes = assertions
            .Select(assertion => new Explanation(assertion, [], []))
            .ToArray();

        var expected = metadataNodes.SelectMany(e => e.Assertions);

        // Act
        var act = metadataNodes.GetAssertions();

        // Assert
        act.Should().BeEquivalentTo(expected);
    }
}
