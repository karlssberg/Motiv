namespace Motiv.Tests;

public class MetadataExtensionsTests
{
    [Theory]
    [AutoData]
    public void Should_gather_metadata_from_a_collection_of_metadata_boolean_results(int[] models)
    {
        // Arrange
        var spec = Spec
            .Build((int n) => n > 0)
            .WhenTrue(m => m)
            .WhenFalse(m => m)
            .Create("is positive");
        
        var booleanResultsCollection = models.Select(spec.IsSatisfiedBy);
        
        // Act
        var act = booleanResultsCollection.GetMetadata();
        
        // Assert
        act.Should().BeEquivalentTo(models);
    }
    
    
    [Theory]
    [AutoData]
    public void Should_gather_metadata_associated_with_true_results(int[] models)
    {
        // Arrange
        var spec = Spec
            .Build((int _) => true)
            .WhenTrue(m => m)
            .WhenFalse(m => m)
            .Create("is always true");
        
        var booleanResultsCollection = models.Select(spec.IsSatisfiedBy);
        
        // Act
        var act = booleanResultsCollection.GetTrueMetadata();
        
        // Assert
        act.Should().BeEquivalentTo(models);
    }
    
    [Theory]
    [AutoData]
    public void Should_gather_metadata_associated_with_false_results(int[] metadata)
    {
        // Arrange
        var spec = Spec
            .Build((int _) => false)
            .WhenTrue(m => m)
            .WhenFalse(m => m)
            .Create("is always false");
        
        var booleanResultsCollection = metadata.Select(spec.IsSatisfiedBy);
        
        // Act
        var act = booleanResultsCollection.GetFalseMetadata();
        
        // Assert
        act.Should().BeEquivalentTo(metadata);
    }
    
    [Theory]
    [AutoData]
    public void Should_gather_metadata_from_a_collection_of_metadata_nodes(IEnumerable<int> numbers)
    {
        // Arrange
        var metadataNodes = numbers
            .Select(n => new MetadataNode<int>(n, Array.Empty<BooleanResultBase<int>>()))
            .ToArray();
        
        var expected = metadataNodes.SelectMany(e => e.Metadata);
        
        // Act
        var act = metadataNodes.GetMetadata();
        
        // Assert
        act.Should().BeEquivalentTo(expected);
    }
}