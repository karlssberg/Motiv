﻿using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class ChangeMetadataSpecTests
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
            .Build<string>(m => isSatisfied)
            .WhenTrue("true before")
            .WhenFalse("false before")
            .CreateSpec();

        var firstSpec = underlying
            .WhenTrue("true after - A")
            .WhenFalse("false after - A");

        var secondSpec = underlying
            .WhenTrue(model => $"true after + {model} - B")
            .WhenFalse("false after - B");

        var thirdSpec = underlying
            .WhenTrue("true after - C")
            .WhenFalse(model => $"false after + {model} - C");

        var fourthSpec = underlying
            .WhenTrue(model => $"true after + {model} - D")
            .WhenFalse(model => $"false after + {model} - D");

        var sut = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var act = sut.IsSatisfiedBy("model");

        act.Reasons.Should().BeEquivalentTo(expectation);
        act.GetMetadata().Should().BeEquivalentTo(expectation);
    }

    [InlineAutoData(true, 1, 3, 5, 7)]
    [InlineAutoData(false, 2, 4, 6, 8)]
    [Theory]
    public void Should_replace_the_metadata_with_new_metadata_type(
        bool isSatisfied,
        int expectedA,
        int expectedB,
        int expectedC,
        int expectedD,
        string trueReason,
        string falseReason)
    {
        int[] expectation = [expectedA, expectedB, expectedC, expectedD];
        var underlying = Spec
            .Build<string>(m => isSatisfied)
            .WhenTrue(trueReason)
            .WhenFalse(falseReason)
            .CreateSpec();

        var firstSpec = Spec
            .Extend(underlying)
            .WhenTrue(1)
            .WhenFalse(2)
            .CreateSpec("first spec");

        var secondSpec = Spec
            .Extend(underlying)
            .WhenTrue(model => 3)
            .WhenFalse(4)
            .CreateSpec("second spec");

        var thirdSpec = Spec
            .Extend(underlying)
            .WhenTrue(5)
            .WhenFalse(model => 6)
            .CreateSpec("third spec");

        var fourthSpec = Spec
            .Extend(underlying)
            .WhenTrue(model => 7)
            .WhenFalse(model => 8)
            .CreateSpec("fourth spec");

        var sut = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var act = sut.IsSatisfiedBy("model");

        act.Reasons.Should().BeEquivalentTo(act.Satisfied
            ? trueReason
            : falseReason);
        
        act.GetMetadata().Should().BeEquivalentTo(expectation);
    }
}