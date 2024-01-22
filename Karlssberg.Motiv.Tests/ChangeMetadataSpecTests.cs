﻿using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class ChangeMetadataSpecTests
{
    [AutoParams(true, "true after - A", "true after + model - B", "true after - C", "true after + model - D")]
    [AutoParams(false, "false after - A", "false after - B", "false after + model - C", "false after + model - D")]
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
            .YieldWhenTrue("true before")
            .YieldWhenFalse("false before")
            .CreateSpec();

        var firstSpec = underlying
            .YieldWhenTrue("true after - A")
            .YieldWhenFalse("false after - A")
            .CreateSpec();

        var secondSpec = underlying
            .YieldWhenTrue(model => $"true after + {model} - B")
            .YieldWhenFalse("false after - B")
            .CreateSpec("second spec");

        var thirdSpec = underlying
            .YieldWhenTrue("true after - C")
            .YieldWhenFalse(model => $"false after + {model} - C")
            .CreateSpec();

        var fourthSpec = underlying
            .YieldWhenTrue(model => $"true after + {model} - D")
            .YieldWhenFalse(model => $"false after + {model} - D")
            .CreateSpec("fourth spec");

        var sut = firstSpec | secondSpec | thirdSpec | fourthSpec;

        var act = sut.IsSatisfiedBy("model");

        act.Reasons.Should().BeEquivalentTo(expectation);
        act.GetMetadata().Should().BeEquivalentTo(expectation);
    }

    [AutoParams(true, 1, 3, 5, 7)]
    [AutoParams(false, 2, 4, 6, 8)]
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
            .YieldWhenTrue(trueReason)
            .YieldWhenFalse(falseReason)
            .CreateSpec();

        var firstSpec = underlying
            .YieldWhenTrue(1)
            .YieldWhenFalse(2)
            .CreateSpec("first spec");

        var secondSpec = underlying
            .YieldWhenTrue(model => 3)
            .YieldWhenFalse(4)
            .CreateSpec("second spec");

        var thridSpec = underlying
            .YieldWhenTrue(5)
            .YieldWhenFalse(model => 6)
            .CreateSpec("third spec");

        var fourthSpec = underlying
            .YieldWhenTrue(model => 7)
            .YieldWhenFalse(model => 8)
            .CreateSpec("fourth spec");

        var sut = firstSpec | secondSpec | thridSpec | fourthSpec;

        var act = sut.IsSatisfiedBy("model");

        act.Reasons.Should().BeEquivalentTo(act.IsSatisfied
            ? trueReason
            : falseReason);
        act.GetMetadata().Should().BeEquivalentTo(expectation);
    }
}