using FluentAssertions;

namespace Motiv.Tests;

public class HigherOrderMetadataSpecTests
{
    public enum Metadata
    {
        Unknown,
        True,
        False
    }
    
    [Theory]
    [InlineData(1, 3, 5, 7, Metadata.False)]
    [InlineData(1, 3, 5, 8, Metadata.False)]
    [InlineData(1, 3, 6, 8, Metadata.True)]
    [InlineData(1, 3, 5, 9, Metadata.False)]
    public void Should_supplant_metadata_from_a_higher_order_spec(int first, int second, int third, int fourth, Metadata expected)
    {
        var underlyingSpec =
            Spec.Build<int>(i => i % 2 == 0)
                .WhenTrue(_ => Guid.NewGuid())
                .WhenFalse(_ => Guid.NewGuid())
                .Create("is even spec");

        var spec =
            Spec.Build(underlyingSpec)
                .AsNSatisfied(2)
                .WhenTrue(Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("is a pair of even numbers");

        var result = spec.IsSatisfiedBy([first, second, third, fourth]);
        
        result.Metadata.Should().BeEquivalentTo([expected]);
    }
    
    [Fact]
    public void Should_preserve_the_description_of_the_underlying_()
    {
        var underlyingSpec =
            Spec.Build<int>(i => i % 2 == 0)
                .WhenTrue(_ => Guid.NewGuid())
                .WhenFalse(_ => Guid.NewGuid())
                .Create("is even spec");

        var spec =
            Spec.Build(underlyingSpec)
                .AsNSatisfied(2)
                .WhenTrue(Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("is a pair of even numbers");

        spec.Statement.Should().Be("is a pair of even numbers");
    }

    [Theory]
    [InlineData(true, true, true, Metadata.True)]
    [InlineData(true, true, false,  Metadata.False)]
    [InlineData(true, false, true, Metadata.False)]
    [InlineData(true, false, false, Metadata.False)]
    [InlineData(false, true, true, Metadata.False)]
    [InlineData(false, true, false, Metadata.False)]
    [InlineData(false, false, true, Metadata.False)]
    [InlineData(false, false, false, Metadata.False)]
    public void Should_only_yield_the_most_recent_when_multiple_yields_are_chained(
        bool first,
        bool second,
        bool third,
        Metadata expectedMetadata)
    {
        var expected = (3, expectedMetadata);
        
        var underlying =
            Spec.Build((bool b) => b)
                .WhenTrue(_ => Guid.NewGuid())
                .WhenFalse(_ => Guid.NewGuid())
                .Create("is true");

        var firstSpec =
            Spec.Build(underlying)
                .AsAllSatisfied()
                .WhenTrue((1, Metadata.True))
                .WhenFalse((1, Metadata.False))
                .Create("first all true");
            
        var secondSpec =
            Spec.Build(firstSpec)
                .WhenTrue((2, Metadata.True))
                .WhenFalse((2, Metadata.False))
                .Create("second all true");
            
        var spec =
            Spec.Build(secondSpec)
                .WhenTrue((3, Metadata.True))
                .WhenFalse((3, Metadata.False))
                .Create("third all true");

        var result = spec.IsSatisfiedBy([first, second, third]);
        
        result.Metadata.Should().BeEquivalentTo([expected]);
    }
    
    [Theory]
    [InlineData(true, true, true, Metadata.True)]
    [InlineData(true, true, false, Metadata.False)]
    [InlineData(true, false, true, Metadata.False)]
    [InlineData(true, false, false, Metadata.False)]
    [InlineData(false, true, true, Metadata.False)]
    [InlineData(false, true, false, Metadata.False)]
    [InlineData(false, false, true, Metadata.False)]
    [InlineData(false, false, false, Metadata.False)]
    public void Should_yield_the_most_deeply_nested_reason_when_requested(
        bool first,
        bool second,
        bool third,
        Metadata expectedMetadata)
    {
        var expected = (0, expectedMetadata);
        var underlying =
            Spec.Build((bool b) => b)
                .WhenTrue(_ => (0, Metadata.True))
                .WhenFalse(_ => (0, Metadata.False))
                .Create("is true");

        var firstSpec =
            Spec.Build(underlying)
                .AsAllSatisfied()
                .WhenTrue((1, Metadata.Unknown))
                .WhenFalse((1, Metadata.Unknown))
                .Create("first all-true encapsulation");
            
        var secondSpec =
            Spec.Build(firstSpec)
                .WhenTrue((2, Metadata.Unknown))
                .WhenFalse((2, Metadata.Unknown))
                .Create("second all-true encapsulation");
            
        var spec =
            Spec.Build(secondSpec)
                .WhenTrue((3, Metadata.Unknown))
                .WhenFalse((3, Metadata.Unknown))
                .Create("third all-true encapsulation");

        var result = spec.IsSatisfiedBy([first, second, third]);
        
        result.RootMetadata.Should().BeEquivalentTo([expected]);
    }
    
    [Theory]
    [InlineData(2, 4, 6, 8, true, Metadata.True)]
    [InlineData(2, 4, 6, 9, false, Metadata.False)]
    [InlineData(1, 4, 6, 9, false, Metadata.False)]
    [InlineData(1, 3, 6, 9, false, Metadata.False)]
    [InlineData(1, 3, 5, 9, false, Metadata.False)]
    public void Should_allow_regular_true_yield_to_be_used_with_a_higher_order_yield_false(
        int first, 
        int second, 
        int third,
        int fourth,
        bool expected,
        Metadata expectedMetadata)
    {
        var underlyingSpec = 
            Spec.Build<int>(i => i % 2 == 0)
                .WhenTrue(_ => Metadata.True)
                .WhenFalse(_ => Metadata.False)
                .Create("is even");

        var spec =
            Spec.Build(underlyingSpec)
                .AsAllSatisfied()
                .WhenTrue(Metadata.True)
                .WhenFalse(results => results.Metadata)
                .Create("all are even");

        var act = spec.IsSatisfiedBy([first, second, third, fourth]);
            
        act.Metadata.Should().BeEquivalentTo([expectedMetadata]);
        act.Satisfied.Should().Be(expected);
    }
    
    
    
    [Theory]
    [InlineData(2, 4, 6, 8, "is even")]
    [InlineData(2, 4, 6, 9, "!is even")]
    [InlineData(1, 4, 6, 9, "!is even")]
    [InlineData(1, 3, 6, 9, "!is even")]
    [InlineData(1, 3, 5, 9, "!is even")]
    public void Should_identify_the_causes_that_yield_metadata_of_the_same_type(
        int first, 
        int second, 
        int third,
        int fourth,
        string expectedMetadata)
    {
        var underlyingSpec = 
            Spec.Build<int>(i => i % 2 == 0)
                .WhenTrue(Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("is even");

        var spec =
            Spec.Build(underlyingSpec)
                .AsAllSatisfied()
                .WhenTrue(Metadata.True)
                .WhenFalse(Metadata.False)
                .Create("all are even");

        var act = spec.IsSatisfiedBy([first, second, third, fourth]);
            
        act.CausesWithMetadata.Should().AllSatisfy(x => x.Reason.Should().Be(expectedMetadata));
    }
}