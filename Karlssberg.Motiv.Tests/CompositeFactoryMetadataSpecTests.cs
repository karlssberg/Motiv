using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class CompositeFactoryMetadataSpecTests
{
    [InlineAutoData(true, "is first true", "is second true", "is third true", "is fourth true")]
    [InlineAutoData(false, "!is first true", "!is second true", "!is third true", "!is fourth true")]
    [Theory]
    public void Should_replace_the_assertion_with_new_assertion(
        bool isSatisfied,
        string expectedA,
        string expectedB,
        string expectedC,
        string expectedD)
    {
        string[] expectation = [expectedA, expectedB, expectedC, expectedD];
        var underlying = Spec
            .Build<string>(m => isSatisfied)
            .WhenTrue(100)
            .WhenFalse(-100)
            .Create("is underlying true");

        var firstSpec = Spec
            .Build(() => underlying)
            .WhenTrue(200)
            .WhenFalse(-200)
            .Create("is first true");

        var secondSpec = Spec
            .Build(() => underlying)
            .WhenTrue(_ => 300)
            .WhenFalse(-300)
            .Create("is second true");

        var thirdSpec = Spec
            .Build(() => underlying)
            .WhenTrue(400)
            .WhenFalse(_ => -400)
            .Create("is third true");

        var fourthSpec = Spec
            .Build(() => underlying)
            .WhenTrue(_ => 500)
            .WhenFalse(_ => -500)
            .Create("is fourth true");

        var sut = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var act = sut.IsSatisfiedBy("model");

        act.Assertions.Should().BeEquivalentTo(expectation);
    }
}