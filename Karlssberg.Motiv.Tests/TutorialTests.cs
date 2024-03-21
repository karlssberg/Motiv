using AutoFixture.Xunit2;
using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class TutorialTests
{
    [Theory]
    [AutoData]
    public void Should_deomo_a_basic_spec()
    {
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .Create("is even");

        isEven.IsSatisfiedBy(2).Satisfied.Should().BeTrue();
        isEven.IsSatisfiedBy(2).Reason.Should().BeEquivalentTo("is even");
        isEven.IsSatisfiedBy(2).Assertions.Should().BeEquivalentTo("is even");

        isEven.IsSatisfiedBy(3).Satisfied.Should().BeFalse();
        isEven.IsSatisfiedBy(3).Reason.Should().BeEquivalentTo("!is even");
        isEven.IsSatisfiedBy(3).Assertions.Should().BeEquivalentTo("!is even");
    }

    [Theory]
    [AutoData]
    public void Should_deomo_a_basic_spec_using_strings_as_assertions()
    {
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .WhenTrue("number is even")
            .WhenFalse("number is odd")
            .Create();

        isEven.IsSatisfiedBy(2).Reason.Should().Be("number is even");
        isEven.IsSatisfiedBy(2).Assertions.Should().BeEquivalentTo("number is even");

        isEven.IsSatisfiedBy(3).Reason.Should().Be("number is odd");
        isEven.IsSatisfiedBy(3).Assertions.Should().BeEquivalentTo("number is odd");
    }

    [Theory]
    [AutoData]
    public void Should_demo_a_basic_spec_using_functions_as_assertion_functions()
    {
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .WhenTrue(n => $"{n} is even")
            .WhenFalse(n => $"{n} is odd")
            .Create("is even");

        isEven.IsSatisfiedBy(2).Reason.Should().Be("is even");
        isEven.IsSatisfiedBy(2).Assertions.Should().BeEquivalentTo("2 is even");
        
        isEven.IsSatisfiedBy(3).Reason.Should().Be("!is even");
        isEven.IsSatisfiedBy(3).Assertions.Should().BeEquivalentTo("3 is odd");
    }

    [Theory]
    [AutoData]
    public void Should_demo_handling_multiple_languages_spec()
    {
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .WhenTrue(n => new { English = "the number is even", Spanish = "el número es par" })
            .WhenFalse(n => new { English = "the number is odd", Spanish = "el número es impar" })
            .Create("is even number");

        isEven.IsSatisfiedBy(2).Satisfied.Should().BeTrue();
        isEven.IsSatisfiedBy(2).Reason.Should().Be("is even number");
        isEven.IsSatisfiedBy(2).Metadata.Select(m => m.English).Should().BeEquivalentTo("the number is even");
        isEven.IsSatisfiedBy(2).Metadata.Select(m => m.Spanish).Should().BeEquivalentTo("el número es par");
    }
    
    [Theory]
    [AutoData]
    public void Should_demo_composite_spec()
    {
        var isPositive = Spec
            .Build<int>(n => n > 0)
            .WhenTrue("the number is positive")
            .WhenFalse(n => $"the number is {(n < 0 ? "negative": "zero")}")
            .Create();

        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .WhenTrue("the number is even")
            .WhenFalse("the number is odd")
            .Create(); 

        var isPositiveAndEven = isPositive & isEven;

        isPositiveAndEven.IsSatisfiedBy(2).Satisfied.Should().BeTrue();
        isPositiveAndEven.IsSatisfiedBy(2).Reason.Should().Be("the number is positive & the number is even");
        isPositiveAndEven.IsSatisfiedBy(2).Assertions.Should().BeEquivalentTo("the number is positive", "the number is even");

        isPositiveAndEven.IsSatisfiedBy(3).Satisfied.Should().BeFalse();
        isPositiveAndEven.IsSatisfiedBy(3).Reason.Should().Be("the number is odd");
        isPositiveAndEven.IsSatisfiedBy(3).Assertions.Should().BeEquivalentTo("the number is odd");

        isPositiveAndEven.IsSatisfiedBy(-2).Satisfied.Should().BeFalse();
        isPositiveAndEven.IsSatisfiedBy(-2).Reason.Should().Be("the number is negative");
        isPositiveAndEven.IsSatisfiedBy(-2).Assertions.Should().BeEquivalentTo("the number is negative");
    }
}