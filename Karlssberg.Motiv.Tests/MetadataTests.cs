using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class MetadataTests
{
    public record Metadata(string Assertion);
    
    [Theory]
    [InlineData(2, "even")]
    [InlineData(3, "odd")]
    public void Should_not_have_duplicate_metadata_from_underlying_metadata_tier(int model, string expectedText)
    {
        var expected = new Metadata(expectedText);
        
        var isEven =
            Spec.Build((int i) => i % 2 == 0)
                .WhenTrue(new Metadata("even"))
                .WhenFalse(new Metadata("odd"))
                .Create("is even");
        
        var allEven = 
            Spec.Build(isEven)
                .AsAllSatisfied()
                .Create("all even");
        
        var firstEven = 
            Spec.Build(allEven)
                .Create("first even");
        
        var secondEven =
            Spec.Build(firstEven)
                .WhenTrue((_, result) => result.Metadata)
                .WhenFalse((_, result) => result.Metadata)
                .Create("second even");
        
        var thirdEven =
            Spec.Build(secondEven)
                .WhenTrue((_, result) => result.Metadata)
                .WhenFalse((_, result) => result.Metadata)
                .Create("third even");
        
        var result = thirdEven.IsSatisfiedBy([model]);
       
        result.MetadataTier.Metadata.Should().BeEquivalentTo([expected]);
        result.MetadataTier.Underlying.GetMetadata().Should().NotBeEquivalentTo([expected]);
        result.MetadataTier.Underlying.SelectMany(explanation => explanation.Underlying).Should().BeEmpty();
    }
}