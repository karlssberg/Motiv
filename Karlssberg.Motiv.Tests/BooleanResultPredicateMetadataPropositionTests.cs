using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class BooleanResultPredicateMetadataPropositionTests
{
    
    [Theory]
    [InlineAutoData(false, false, true, "is first true", "is second true", "is third true", "is fourth true")]
    [InlineAutoData(false, true, false, "!is first true", "!is second true", "!is third true", "!is fourth true")]
    [InlineAutoData(true, false, false, "!is first true", "!is second true", "!is third true", "!is fourth true")]
    [InlineAutoData(false, false, true, "is first true", "is second true", "is third true", "is fourth true")]
    public void Should_replace_the_metadata_with_new_metadata(
        bool model,
        bool other,
        bool expected,
        string expectedA,
        string expectedB,
        string expectedC,
        string expectedD)
    {
        string[] expectation = [expectedA, expectedB, expectedC, expectedD];;
        var underlying = Spec
            .Build((bool m) => m == other)
            .WhenTrue(100)
            .WhenFalse(-100)
            .Create($"are equal");
        
        var firstSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue(200)
            .WhenFalse(-200)
            .Create("is first true");

        var secondSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue(_ => 300)
            .WhenFalse(-300)
            .Create("is second true");

        var thirdSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue(400)
            .WhenFalse(_ => -400)
            .Create("is third true");

        var fourthSpec = Spec
            .Build((bool m) => underlying.IsSatisfiedBy(m))
            .WhenTrue(_ => 500)
            .WhenFalse(_ => -500)
            .Create("is fourth true");

        var sut = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var act = sut.IsSatisfiedBy(model);

        act.Satisfied.Should().Be(expected);
        act.Assertions.Should().BeEquivalentTo(expectation);
    }
}