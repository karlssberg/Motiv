﻿using FluentAssertions;
using Humanizer;

namespace Karlssberg.Motiv.Tests;

public class ChangeHigherOrderMetadataSpecTests
{
    [Theory]
    [AutoParams(1, 3, 5, 7, "is not a pair of even numbers")]
    [AutoParams(1, 3, 5, 8, "is not a pair of even numbers")]
    [AutoParams(1, 3, 6, 8, "is a pair of even numbers")]
    [AutoParams(1, 3, 5, 7, "is not a pair of even numbers")]
    public void Should_supplant_metadata_from_a_higher_order_spec(int first, int second, int third, int fourth, string expected)
    {
        var underlyingSpec = Spec
            .Build<int>(i => i % 2 == 0)
            .YieldWhenTrue(i => $"{i} is even")
            .YieldWhenFalse(i => $"{i} is odd")
            .CreateSpec("is even spec");

        var sut = underlyingSpec
            .Exactly(2, "a pair of even numbers")
            .YieldWhenTrue("is a pair of even numbers")
            .YieldWhenFalse("is not a pair of even numbers");

        var result = sut.IsSatisfiedBy([first, second, third, fourth]);
        
        result.Reasons.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void Should_preserve_the_description_of_the_underlying_()
    {
        var underlyingSpec = Spec
            .Build<int>(i => i % 2 == 0)
            .YieldWhenTrue("is even")
            .YieldWhenFalse("is odd")
            .CreateSpec("is even spec");

        var sut = underlyingSpec
            .Exactly(2, "a pair of even numbers")
            .YieldWhenTrue((_, _) => "is a pair of even numbers")
            .YieldWhenFalse((_, _) => "is not a pair of even numbers");

        sut.Description.Should().Be("<a pair of even numbers>(is even spec)");
    }

    [Theory]
    [AutoParams(true, true, true, "third true yield")]
    [AutoParams(true, true, false, "third false yield")]
    [AutoParams(true, false, true, "third false yield")]
    [AutoParams(true, false, false, "third false yield")]
    [AutoParams(false, true, true, "third false yield")]
    [AutoParams(false, true, false, "third false yield")]
    [AutoParams(false, false, true, "third false yield")]
    [AutoParams(false, false, false, "third false yield")]
    public void Should_only_yield_the_most_recent_when_multiple_yields_are_chained(bool first, bool second, bool third, string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(b => b)
            .YieldWhenTrue("is even")
            .YieldWhenFalse("is odd")
            .CreateSpec("is even spec");

        var sut = underlyingSpec
            .All("a pair of even numbers")
            .YieldWhenTrue((_, _) => "first true yield")
            .YieldWhenFalse((_, _) => "first false yield")
            .YieldWhenTrue((_, _) => "second true yield")
            .YieldWhenFalse((_, _) => "second false yield")
            .YieldWhenTrue((_, _) => "third true yield")
            .YieldWhenFalse((_, _) => "third false yield");

        var result = sut.IsSatisfiedBy([first, second, third]);
        
        result.Reasons.Should().BeEquivalentTo(expected);
    }
    
    [Theory]
    [AutoParams(true, true, true, "is even")]
    [AutoParams(true, true, false, "is odd")]
    [AutoParams(true, false, true, "is odd")]
    [AutoParams(true, false, false, "is odd")]
    [AutoParams(false, true, true, "is odd")]
    [AutoParams(false, true, false, "is odd")]
    [AutoParams(false, false, true, "is odd")]
    [AutoParams(false, false, false, "is odd")]
    public void Should_yield_the_most_deeply_nested_reason_when_requested(bool first, bool second, bool third, string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(b => b)
            .YieldWhenTrue("is even")
            .YieldWhenFalse("is odd")
            .CreateSpec("is even spec");

        var sut = underlyingSpec
            .All("a pair of even numbers")
            .YieldWhenTrue((_, _) => "first true yield")
            .YieldWhenFalse((_, _) => "first false yield")
            .YieldWhenTrue((_, _) => "second true yield")
            .YieldWhenFalse((_, _) => "second false yield")
            .YieldWhenTrue((_, _) => "third true yield")
            .YieldWhenFalse((_, _) => "third false yield");

        var result = sut.IsSatisfiedBy([first, second, third]);
        
        result.GetRootCauses().Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Should_allow_regular_true_yield_to_be_used_with_a_higher_order_yield_false()
    {
        var underlyingSpec = Spec
            .Build<int>(i => i % 2 == 0)
            .YieldWhenTrue(i => $"{i} is even")
            .YieldWhenFalse(i => $"{i} is odd")
            .CreateSpec("is even spec");
        
        var sut = underlyingSpec
            .All("all even")
            .YieldWhenTrue("all even")
            .YieldWhenFalse((_, unsatisfied) => $"not all even, {unsatisfied.Humanize()} are odd");

    }
}