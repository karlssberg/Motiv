using FluentAssertions;

namespace Karlssberg.Motive.Tests;

public class SubstituteMetadataSpecificationTests
{
    [AutoParams(true, "true after - A", "true after + model - B", "true after - C", "true after + model - D")]
    [AutoParams(false, "false after - A", "false after - B", "false after + model - C", "false after + model - D")]
    [Theory]
    public void Should_replace_the_metadata_with_new_metadata(bool isSatisfied, string expectedA, string expectedB, string expectedC, string expectedD)
    {
        IEnumerable<string> expectation = [expectedA, expectedB, expectedC, expectedD];
        var underlying = new Spec<string, string>(
            "original",
            _ => isSatisfied,
            "true before",
            "false before");

        var sut = underlying.SubstituteMetadata("true after - A", "false after - A")
            | underlying.SubstituteMetadata(model => $"true after + {model} - B", "false after - B")
            | underlying.SubstituteMetadata("true after - C", model => $"false after + {model} - C")
            | underlying.SubstituteMetadata(model => $"true after + {model} - D", model => $"false after + {model} - D");

        var act = sut.IsSatisfiedBy("model");

        act.Reasons.Should().BeEquivalentTo(expectation);
        act.GetInsights().Should().BeEquivalentTo(expectation);
    }
}