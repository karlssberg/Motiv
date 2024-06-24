namespace Motiv.Tests;

public class MetadataTests
{
    private record Metadata(string Assertion)
    {
        public string Assertion { get; } = Assertion;
    }

    [Theory]
    [InlineData(2, "even")]
    [InlineData(3, "odd")]
    public void Should_not_have_duplicate_metadata_from_underlying_metadata_tier(int model, string expected)
    {
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
                .WhenTrueYield((_, result) => result.Metadata)
                .WhenFalseYield((_, result) => result.Metadata)
                .Create("second even");
        
        var thirdEven =
            Spec.Build(secondEven)
                .WhenTrueYield((_, result) => result.Metadata)
                .WhenFalseYield((_, result) => result.Metadata)
                .Create("third even");
        
        var result = thirdEven.IsSatisfiedBy([model]);

        var act = result.MetadataTier.Metadata.Select(metadata => metadata.Assertion);
        
        act.Should().BeEquivalentTo([expected]);
    }
    
    [Theory]
    [InlineData(2, "even")]
    [InlineData(3, "odd")]
    public void Should_not_have_duplicate_underlying_metadata_from_underlying_metadata_tier(int model, string expected)
    {
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
                .WhenTrueYield((_, result) => result.Metadata)
                .WhenFalseYield((_, result) => result.Metadata)
                .Create("second even");
        
        var thirdEven =
            Spec.Build(secondEven)
                .WhenTrueYield((_, result) => result.Metadata)
                .WhenFalseYield((_, result) => result.Metadata)
                .Create("third even");
        
        var result = thirdEven.IsSatisfiedBy([model]);

        var act = result.MetadataTier.Underlying.GetMetadata();
        
        act.Should().NotBeEquivalentTo(expected);
    }
    
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Should_not_have_yield_metadata_from_levels_that_do_not_exist(int model)
    {
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
                .WhenTrueYield((_, result) => result.Metadata)
                .WhenFalseYield((_, result) => result.Metadata)
                .Create("second even");
        
        var thirdEven =
            Spec.Build(secondEven)
                .WhenTrueYield((_, result) => result.Metadata)
                .WhenFalseYield((_, result) => result.Metadata)
                .Create("third even");
        
        var result = thirdEven.IsSatisfiedBy([model]);

        var act = result.MetadataTier.Underlying.SelectMany(metadata => metadata.Underlying);
        
        act.Should().BeEmpty();
    }
}