using FluentAssertions;

namespace Motiv.Tests;

public class AsNSatisfiedSpecTests
{
    [Theory]
    [InlineAutoData(2, 4, true)]
    [InlineAutoData(4, 3, false)]
    [InlineAutoData(1, 2, false)]
    [InlineAutoData(1, 3, false)]
    public void Should_perform_the_logical_operation_NSatisfied(
        int first,
        int second,
        bool expected)
    {
        var isEven = Spec
            .Build<int>(i => i % 2 == 0)
            .WhenTrue(i => $"{i} is even")
            .WhenFalse(i => $"{i} is odd")
            .Create("is even spec");

        var sut = Spec
            .Build(isEven)
            .AsNSatisfied(2)
            .WhenTrue("is a pair of even numbers")
            .WhenFalse("is not a pair of even numbers")
            .Create();

        var result = sut.IsSatisfiedBy([first, second]);

        result.Satisfied.Should().Be(expected);
    }
    
    [Theory]
    [InlineAutoData(1, 3, 5, 7, false, "The pack does not contain exactly a pair of even numbers", "1 is odd, 3 is odd, 5 is odd, 7 is odd")]
    [InlineAutoData(1, 3, 5, 6, false, "The pack does not contain exactly a pair of even numbers", "6 is even")]
    [InlineAutoData(1, 3, 4, 6, true, "4, 6 are a pair of even numbers", "4 is even, 6 is even")]
    [InlineAutoData(1, 4, 6, 8, false, "The pack does not contain exactly a pair of even numbers", "4 is even, 6 is even, 8 is even")]
    public void Should_mirror_the_outcome_of_an_all_satisfied_spec_metadata(
        int first,
        int second,
        int third,
        int fourth,
        bool expected,
        string expectedShallowAssertionSerialized,
        string expectedDeepAssertionsSerialized)
    {
        var expectedDeepAssertions = expectedDeepAssertionsSerialized.Split(new []{", "}, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .WhenTrue(n => $"{n} is even")
            .WhenFalse(n => $"{n} is odd")
            .Create("is even spec");

        var sut = Spec
            .Build(isEven)
            .AsNSatisfied(2)
            .WhenTrue((results) =>
                $"{string.Join(", ", results.CausalModels)} are a pair of even numbers")
            .WhenFalse("The pack does not contain exactly a pair of even numbers")
            .Create("a pair of even numbers");

        var result = sut.IsSatisfiedBy([first, second, third, fourth]);

        result.Satisfied.Should().Be(expected);
        result.Explanation.Assertions.Should().BeEquivalentTo(expectedShallowAssertionSerialized);
        result.Explanation.Underlying.GetAssertions().Should().BeEquivalentTo(expectedDeepAssertions);
    }
    
    [Theory]
    [InlineAutoData(true, true, "2 even")]
    [InlineAutoData(true, false, "1 even and 1 odd")]
    [InlineAutoData(false, true, "1 even and 1 odd")]
    [InlineAutoData(false, false, "0 even and 2 odd")]
    public void Should_serialize_the_result_of_the_NSatisfied_operation_when_metadata_is_a_string(
        bool first,
        bool second,
        string expected)
    {
        var isEven = Spec
            .Build((bool m) => m)
            .WhenTrue(true.ToString().ToLowerInvariant())
            .WhenFalse(false.ToString().ToLowerInvariant())
            .Create();

        var sut = Spec
            .Build(isEven)
            .AsNSatisfied(2)
            .WhenTrue("2 even")
            .WhenFalse(evaluation => $"{evaluation.TrueCount} even and {evaluation.FalseCount} odd")
            .Create();
        
        var result = sut.IsSatisfiedBy([first, second]);

        result.Description.Reason.Should().Be(expected);
    }
    
    [Fact]
    public void Should_describe_an_NSatisfied_spec()
    {
        var isEven = Spec
            .Build<int>(n => n % 2 == 0)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("is even");

        var sut = Spec
            .Build(isEven)
            .AsNSatisfied(2)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("a pair of even numbers");

        sut.Statement.Should().Be("a pair of even numbers");
    }
    
    [Theory]
    [InlineData(false, false, false, false, "!2 are true")]
    [InlineData(false, false, true, false, "!2 are true")]
    [InlineData(false, true, false, false, "!2 are true")]
    [InlineData(false, true, true, true, "2 are true")]
    [InlineData(true, false, false, false, "!2 are true")]
    [InlineData(true, false, true, true, "2 are true")]
    [InlineData(true, true, false, true, "2 are true")]
    [InlineData(true, true, true, false, "!2 are true")]
    public void Should_perform_a_none_satisfied_operation_when_using_a_boolean_predicate_function(
        bool first,
        bool second,
        bool third,
        bool expected,
        string expectedReason)
    {

        bool[] models = [first, second, third];

        var sut =
            Spec.Build((bool m) => m)
                .AsNSatisfied(2)
                .Create("2 are true");

        var result = sut.IsSatisfiedBy(models);

        result.Satisfied.Should().Be(expected);
        result.Reason.Should().Be(expectedReason);
    }
    
    [Theory]
    [InlineData(false, false, false, false, "!2 are true")]
    [InlineData(false, false, true, false, "!2 are true")]
    [InlineData(false, true, false, false, "!2 are true")]
    [InlineData(false, true, true, true, "2 are true")]
    [InlineData(true, false, false, false, "!2 are true")]
    [InlineData(true, false, true, true, "2 are true")]
    [InlineData(true, true, false, true, "2 are true")]
    [InlineData(true, true, true, false, "!2 are true")]
    public void Should_perform_an_n_satisfied_operation_when_using_a_boolean_result_predicate_function_with_metadata(
        bool first,
        bool second,
        bool third,
        bool expected,
        string expectedReason)
    {

        bool[] models = [first, second, third];

        var sut =
            Spec.Build((bool m) => m)
                .AsNSatisfied(2)
                .WhenTrue(_ => "2 are true")
                .WhenFalse(_ => "!2 are true")
                .Create("none are true");

        var result = sut.IsSatisfiedBy(models);

        result.Satisfied.Should().Be(expected);
        result.Reason.Should().Be(expectedReason);
    }
    
    [Theory]
    [InlineData(false, false, false, false, "!2 are true")]
    [InlineData(false, false, true, false, "!2 are true")]
    [InlineData(false, true, false, false, "!2 are true")]
    [InlineData(false, true, true, true, "2 are true")]
    [InlineData(true, false, false, false, "!2 are true")]
    [InlineData(true, false, true, true, "2 are true")]
    [InlineData(true, true, false, true, "2 are true")]
    [InlineData(true, true, true, false, "!2 are true")]
    public void Should_perform_an_n_satisfied_operation_when_using_a_boolean_result_predicate_function(
        bool first,
        bool second,
        bool third,
        bool expected,
        string expectedReason)
    {
        var underlying =
            Spec.Build((bool m) => m)
                .Create("is true");

        bool[] models = [first, second, third];

        var sut =
            Spec.Build((bool model) => underlying.IsSatisfiedBy(model))
                .AsNSatisfied(2)
                .WhenTrue(_ => "2 are true")
                .WhenFalse(_ => "!2 are true")
                .Create("2 are true");

        var result = sut.IsSatisfiedBy(models);

        result.Satisfied.Should().Be(expected);
        result.Reason.Should().Be(expectedReason);
    }
}