using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class ExplanationTests
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
        result.Explanation.Assertions.Should().ContainSingle(expected);
    }
    
    [Fact]
    public void Should_harvest_assertions_from_underlying()
    {
        var isEvenSpecAtRootDepth = 
            Spec.Build((long n) => n % 2 == 0)
                .WhenTrue("even")
                .WhenFalse("odd")
                .Create();

        var isPositiveSpecAtRootDepth =
            Spec.Build((decimal n) => n > 0)
                .WhenTrue("positive")
                .WhenFalse("not positive")
                .Create();
        
        var isEvenWrapperSpecAtDepth2 = 
            Spec.Build(isEvenSpecAtRootDepth)
                .WhenTrue("even wrapper")
                .WhenFalse("odd wrapper")
                .Create();

        var isPositiveWrapperSpecAtDepth2 =
            Spec.Build(isPositiveSpecAtRootDepth)
                .WhenTrue("positive wrapper")
                .WhenFalse("not positive wrapper")
                .Create();

        var isEvenAndPositiveUsingPredicateSpecAtDepth1 = 
            Spec.Build((int n) => isEvenWrapperSpecAtDepth2.IsSatisfiedBy(n) & isPositiveWrapperSpecAtDepth2.IsSatisfiedBy(n))
                .WhenTrue("even and positive from predicate")
                .WhenFalse("not even and positive from predicate")
                .Create();
        
        var isEvenAndPositiveUsingChangeModelSpecAtDepth1 = 
            Spec.Build((int _) => 
                    isEvenWrapperSpecAtDepth2.ChangeModelTo<int>(i => i) & 
                    isPositiveWrapperSpecAtDepth2.ChangeModelTo<int>(i => i))
                .WhenTrue("even and positive from change model method")
                .WhenFalse("not even and positive from change model method")
                .Create();
        
        var isEvenWholeNumber = 
            Spec.Build(isEvenAndPositiveUsingPredicateSpecAtDepth1 & isEvenAndPositiveUsingChangeModelSpecAtDepth1)
                .WhenTrue("even whole number")
                .WhenFalse("not an even whole number")
                .Create();
        
        isEvenWholeNumber.IsSatisfiedBy(2).AllRootAssertions.Should().BeEquivalentTo("even", "positive");
        isEvenWholeNumber.IsSatisfiedBy(2).RootAssertions.Should().BeEquivalentTo("even", "positive");
        
        isEvenWholeNumber.IsSatisfiedBy(3).AllRootAssertions.Should().BeEquivalentTo("odd", "positive");
        isEvenWholeNumber.IsSatisfiedBy(3).RootAssertions.Should().BeEquivalentTo("odd");
        
        isEvenWholeNumber.IsSatisfiedBy(0).AllRootAssertions.Should().BeEquivalentTo("even", "not positive");        
        isEvenWholeNumber.IsSatisfiedBy(0).RootAssertions.Should().BeEquivalentTo("not positive");
        
        isEvenWholeNumber.IsSatisfiedBy(-3).AllRootAssertions.Should().BeEquivalentTo("odd", "not positive");
        isEvenWholeNumber.IsSatisfiedBy(-3).RootAssertions.Should().BeEquivalentTo("odd", "not positive");
        
        isEvenWholeNumber.IsSatisfiedBy(2).GetAllAssertionsAtDepth(2).Should().BeEquivalentTo("even wrapper", "positive wrapper");
        isEvenWholeNumber.IsSatisfiedBy(3).GetAllAssertionsAtDepth(2).Should().BeEquivalentTo("odd wrapper", "positive wrapper");
        isEvenWholeNumber.IsSatisfiedBy(0).GetAllAssertionsAtDepth(2).Should().BeEquivalentTo("even wrapper", "not positive wrapper");
        isEvenWholeNumber.IsSatisfiedBy(-3).GetAllAssertionsAtDepth(2).Should().BeEquivalentTo("odd wrapper", "not positive wrapper");
        
        isEvenWholeNumber.IsSatisfiedBy(2).GetAllAssertionsAtDepth(1).Should().BeEquivalentTo("even and positive from predicate", "even and positive from change model method");
        isEvenWholeNumber.IsSatisfiedBy(2).SubAssertions.Should().BeEquivalentTo("even and positive from predicate", "even and positive from change model method");
        
        isEvenWholeNumber.IsSatisfiedBy(2).SubAssertions.Should().BeEquivalentTo("even and positive from predicate", "even and positive from change model method");
        
        isEvenWholeNumber.IsSatisfiedBy(0).GetAllAssertionsAtDepth(1).Should().BeEquivalentTo("not even and positive from predicate", "not even and positive from change model method");
        isEvenWholeNumber.IsSatisfiedBy(0).SubAssertions.Should().BeEquivalentTo("not even and positive from predicate", "not even and positive from change model method");
        
        isEvenWholeNumber.IsSatisfiedBy(2).GetAllAssertionsAtDepth(0).Should().BeEquivalentTo("even whole number");
        isEvenWholeNumber.IsSatisfiedBy(2).Assertions.Should().BeEquivalentTo("even whole number");
        
        isEvenWholeNumber.IsSatisfiedBy(0).GetAllAssertionsAtDepth(0).Should().BeEquivalentTo("not an even whole number");
        isEvenWholeNumber.IsSatisfiedBy(0).Assertions.Should().BeEquivalentTo("not an even whole number");
    }

    [Theory]
    [InlineData(6, "even", "positive", "divisible by 3")]
    [InlineData(4, "even", "positive", "not divisible by 3")]
    [InlineData(3, "odd", "positive", "divisible by 3")]
    [InlineData(1, "odd", "positive", "not divisible by 3")]
    [InlineData(0, "even", "not positive", "divisible by 3")]
    [InlineData(-3, "odd", "not positive", "divisible by 3")]
    [InlineData(-5, "odd", "not positive", "not divisible by 3")]
    public void Should_yeild_the_assertions_from_all_operands_when_using_all_assertions_property(int n, params string[] expected)
    {
        var isEvenSpec =
            Spec.Build<int>(i => i % 2 == 0)
                .WhenTrue("even")
                .WhenFalse("odd")
                .Create();
        
        var isPositiveSpec =
            Spec.Build<int>(n => n > 0)
                .WhenTrue("positive")
                .WhenFalse("not positive")
                .Create();
            
        var isDivisibleBy3Spec =
            Spec.Build<int>(n => n % 3 == 0)
                .WhenTrue("divisible by 3")
                .WhenFalse("not divisible by 3")
                .Create();
        
        var spec = isEvenSpec & isPositiveSpec & isDivisibleBy3Spec;
        
        var result = spec.IsSatisfiedBy(n);
        
        result.AllAssertions.Should().BeEquivalentTo(expected);
    }
    
    
    [Theory]
    [InlineData(6, "even", "positive", "divisible by 3")]
    [InlineData(4, "not divisible by 3")]
    [InlineData(3, "odd")]
    [InlineData(1, "odd", "not divisible by 3")]
    [InlineData(0, "not positive")]
    [InlineData(-3, "odd", "not positive")]
    [InlineData(-5, "odd", "not positive", "not divisible by 3")]
    public void Should_yeild_the_assertions_from_determinate_operands_when_using_sub_assertions(
        int n,
        params string[] expected)
    {
        var isEvenSpec =
            Spec.Build<int>(i => i % 2 == 0)
                .WhenTrue("even")
                .WhenFalse("odd")
                .Create();
        
        var isPositiveSpec =
            Spec.Build<int>(n => n > 0)
                .WhenTrue("positive")
                .WhenFalse("not positive")
                .Create();
        
        var isDivisibleBy3Spec =
            Spec.Build<int>(n => n % 3 == 0)
                .WhenTrue("divisible by 3")
                .WhenFalse("not divisible by 3")
                .Create();
        
        var spec = 
            Spec.Build(isEvenSpec & isPositiveSpec & isDivisibleBy3Spec)
                .WhenTrue("even, positive, and divisible by 3")
                .WhenFalse("not even, positive, or divisible by 3")
                .Create();
        
        var result = spec.IsSatisfiedBy(n);
        
        result.SubAssertions.Should().BeEquivalentTo(expected);
    }
    
    [Theory]
    [InlineData(6, "even", "positive", "divisible by 3")]
    [InlineData(4, "even", "positive", "not divisible by 3")]
    [InlineData(3, "odd", "positive", "divisible by 3")]
    [InlineData(1, "odd", "positive", "not divisible by 3")]
    [InlineData(0, "even", "not positive", "divisible by 3")]
    [InlineData(-3, "odd", "not positive", "divisible by 3")]
    [InlineData(-5, "odd", "not positive", "not divisible by 3")]
    public void Should_yeild_the_assertions_from_determinate_operands_when_using_all_sub_assertions(
        int n,
        params string[] expected)
    {
        var isEvenSpec =
            Spec.Build<int>(i => i % 2 == 0)
                .WhenTrue("even")
                .WhenFalse("odd")
                .Create();
        
        var isPositiveSpec =
            Spec.Build<int>(n => n > 0)
                .WhenTrue("positive")
                .WhenFalse("not positive")
                .Create();
        
        var isDivisibleBy3Spec =
            Spec.Build<int>(n => n % 3 == 0)
                .WhenTrue("divisible by 3")
                .WhenFalse("not divisible by 3")
                .Create();
        
        var spec = 
            Spec.Build(isEvenSpec & isPositiveSpec & isDivisibleBy3Spec)
                .WhenTrue("even, positive, and divisible by 3")
                .WhenFalse("not even, positive, or divisible by 3")
                .Create();
        
        var result = spec.IsSatisfiedBy(n);
        
        result.AllSubAssertions.Should().BeEquivalentTo(expected);
    }
} 