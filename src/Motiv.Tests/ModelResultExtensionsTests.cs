using Motiv.HigherOrderProposition;

namespace Motiv.Tests;

public class ModelResultExtensionsTests
{
    private static ModelResult<int>[] ModelResults(params bool[] satisfied) =>
        satisfied.Select((s, i) => new ModelResult<int>(i, s)).ToArray();

    // A non-array, non-IReadOnlyList sequence over the same data, forcing the LINQ
    // fallback branch of each extension method (the array fast-path is exercised by
    // asserting on the array itself).
    private static IEnumerable<ModelResult<int>> Lazy(ModelResult<int>[] results) =>
        results.Select(result => result);

    [Theory]
    [InlineData(new[] { true, true, false }, 2)]
    [InlineData(new[] { false, false, false }, 0)]
    [InlineData(new bool[0], 0)]
    public void Should_count_satisfied_results(bool[] satisfied, int expected)
    {
        var array = ModelResults(satisfied);

        array.CountTrue().ShouldBe(expected);
        Lazy(array).CountTrue().ShouldBe(expected);
    }

    [Theory]
    [InlineData(new[] { true, true, false }, 1)]
    [InlineData(new[] { true, true, true }, 0)]
    [InlineData(new bool[0], 0)]
    public void Should_count_unsatisfied_results(bool[] satisfied, int expected)
    {
        var array = ModelResults(satisfied);

        array.CountFalse().ShouldBe(expected);
        Lazy(array).CountFalse().ShouldBe(expected);
    }

    [Theory]
    [InlineData(new[] { true, true, true }, true)]
    [InlineData(new[] { true, false, true }, false)]
    [InlineData(new bool[0], true)]
    public void Should_report_whether_all_results_are_satisfied(bool[] satisfied, bool expected)
    {
        var array = ModelResults(satisfied);

        array.AllTrue().ShouldBe(expected);
        Lazy(array).AllTrue().ShouldBe(expected);
    }

    [Theory]
    [InlineData(new[] { false, false, false }, true)]
    [InlineData(new[] { false, true, false }, false)]
    [InlineData(new bool[0], true)]
    public void Should_report_whether_all_results_are_unsatisfied(bool[] satisfied, bool expected)
    {
        var array = ModelResults(satisfied);

        array.AllFalse().ShouldBe(expected);
        Lazy(array).AllFalse().ShouldBe(expected);
    }

    [Theory]
    [InlineData(new[] { false, true, false }, true)]
    [InlineData(new[] { false, false, false }, false)]
    [InlineData(new bool[0], false)]
    public void Should_report_whether_any_result_is_satisfied(bool[] satisfied, bool expected)
    {
        var array = ModelResults(satisfied);

        array.AnyTrue().ShouldBe(expected);
        Lazy(array).AnyTrue().ShouldBe(expected);
    }

    [Theory]
    [InlineData(new[] { true, false, true }, true)]
    [InlineData(new[] { true, true, true }, false)]
    [InlineData(new bool[0], false)]
    public void Should_report_whether_any_result_is_unsatisfied(bool[] satisfied, bool expected)
    {
        var array = ModelResults(satisfied);

        array.AnyFalse().ShouldBe(expected);
        Lazy(array).AnyFalse().ShouldBe(expected);
    }
}
