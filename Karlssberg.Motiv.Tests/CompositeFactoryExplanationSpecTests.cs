using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class CompositeFactoryExplanationSpecTests
{
    
    [InlineAutoData(true, "true after - A", "true after + model - B", "true after - C", "true after + model - D")]
    [InlineAutoData(false, "false after - A", "false after - B", "false after + model - C", "false after + model - D")]
    [Theory]
    public void Should_replace_the_metadata_with_new_metadata(
        bool isSatisfied,
        string expectedA,
        string expectedB,
        string expectedC,
        string expectedD)
    {
        string[] expectation = [expectedA, expectedB, expectedC, expectedD];
        var underlying = Spec
            .Build<string>(_ => isSatisfied)
            .WhenTrue("true before")
            .WhenFalse("false before")
            .Create();

        var firstSpec = Spec
            .Build(() => underlying)
            .WhenTrue("true after - A")
            .WhenFalse("false after - A")
            .Create();

        var secondSpec = Spec
            .Build(() => underlying)
            .WhenTrue(model => $"true after + {model} - B")
            .WhenFalse("false after - B")
            .Create("is second true");

        var thirdSpec = Spec
            .Build(() => underlying)
            .WhenTrue("true after - C")
            .WhenFalse(model => $"false after + {model} - C")
            .Create();

        var fourthSpec = Spec
            .Build(() => underlying)
            .WhenTrue(model => $"true after + {model} - D")
            .WhenFalse(model => $"false after + {model} - D")
            .Create("true after + model - D");

        var sut = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var act = sut.IsSatisfiedBy("model");

        act.Assertions.Should().BeEquivalentTo(expectation);
        act.MetadataTree.Should().BeEquivalentTo(expectation);
    }
}