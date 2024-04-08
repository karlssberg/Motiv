using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class BooleanResultPredicateExplanationPropositionTests
{
    
    [Theory]
    [InlineAutoData(false, false, true, "underlying is true", "first is true", "second is true", "third is true", "fourth is true")]
    [InlineAutoData(false, true, false, "underlying is false", "first is false", "second is false", "third is false", "fourth is false")]
    [InlineAutoData(true, false, false, "underlying is false", "first is false", "second is false", "third is false", "fourth is false")]
    [InlineAutoData(false, false, true, "underlying is true", "first is true", "second is true", "third is true", "fourth is true")]
    public void Should_replace_the_assertion_with_new_assertion(
        bool model,
        bool other,
        bool expected,
        string expectedRootAssertion,
        string expectedA,
        string expectedB,
        string expectedC,
        string expectedD)
    {
        string[] expectation = [expectedA, expectedB, expectedC, expectedD];;
        var underlying = Spec
            .Build((bool m) => m == other)
            .WhenTrue("underlying is true")
            .WhenFalse("underlying is false")
            .Create("are equal");
        
        var firstSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue("first is true")
            .WhenFalse("first is false")
            .Create();

        var secondSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue(_ => "second is true")
            .WhenFalse("second is false")
            .Create("is second true");

        var thirdSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue("third is true")
            .WhenFalse(_ => "third is false")
            .Create("is third true");

        var fourthSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue(_ => "fourth is true")
            .WhenFalse(_ => "fourth is false")
            .Create("is fourth true");

        var sut = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var act = sut.IsSatisfiedBy(model);
        
        act.Satisfied.Should().Be(expected);
        act.GetRootAssertions().Should().BeEquivalentTo(expectedRootAssertion);
        act.Assertions.Should().BeEquivalentTo(expectation);
    }
}