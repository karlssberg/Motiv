﻿using FluentAssertions;

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
} 