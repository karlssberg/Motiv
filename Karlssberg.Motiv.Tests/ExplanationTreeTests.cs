using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class ExplanationTreeTests
{
    [Theory]
    [InlineAutoData(1, "is odd")]
    [InlineAutoData(2, "is even")]
    public void Should_provide_a_reason_for_a_spec_result(int n, string expected)
    {
        var spec = Spec.Build<int>(i => i % 2 == 0)
            .WhenTrue("is even")
            .WhenFalse("is odd")
            .Create();
        
        var result = spec.IsSatisfiedBy(n);
        result.ExplanationTree.Assertions.Should().ContainSingle(expected);
    }
    
    [Fact]
    public void Should_harvest_assertions_from_underlying()
    {
        var isEvenSpec = 
            Spec.Build((long n) => n % 2 == 0)
                .WhenTrue("even")
                .WhenFalse("odd")
                .Create();

        var isPositiveSpec =
            Spec.Build((decimal n) => n > 0)
                .WhenTrue("positive")
                .WhenFalse("not positive")
                .Create();
        
        var isEvenWrapperSpec = 
            Spec.Build(isEvenSpec)
                .WhenTrue("even wrapper")
                .WhenFalse("odd wrapper")
                .Create();

        var isPositiveWrapperSpec =
            Spec.Build(isPositiveSpec)
                .WhenTrue("positive wrapper")
                .WhenFalse("not positive wrapper")
                .Create();

        var isEvenAndPositiveSpec = 
            Spec.Build((int n) => isEvenWrapperSpec.IsSatisfiedBy(n) & isPositiveWrapperSpec.IsSatisfiedBy(n))
                .WhenTrue("even and positive")
                .WhenFalse("not even and positive")
                .Create();
        
        var allGood = 
            Spec.Build(isEvenAndPositiveSpec)
                .WhenTrue("all good")
                .WhenFalse("not good")
                .Create();
        
        allGood.IsSatisfiedBy(2).AllRootAssertions.Should().BeEquivalentTo("even", "positive");
        allGood.IsSatisfiedBy(3).AllRootAssertions.Should().BeEquivalentTo("odd", "positive");
        allGood.IsSatisfiedBy(0).AllRootAssertions.Should().BeEquivalentTo("even", "not positive");
        allGood.IsSatisfiedBy(-3).AllRootAssertions.Should().BeEquivalentTo("odd", "not positive");
        allGood.IsSatisfiedBy(2).AllSubAssertions.Should().BeEquivalentTo("even and positive");
        allGood.IsSatisfiedBy(0).AllSubAssertions.Should().BeEquivalentTo("not even and positive");
        allGood.IsSatisfiedBy(2).Assertions.Should().BeEquivalentTo("all good");
        allGood.IsSatisfiedBy(0).Assertions.Should().BeEquivalentTo("not good");
    }
} 