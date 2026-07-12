using Motiv.HigherOrderProposition;

namespace Motiv.Tests;

public class HigherOrderShortCircuitTests
{
    private sealed class Counter { public int Count; }

    // Counting projection: state is the Counter so the lambda passed to Evaluate stays static.
    private static bool Positive(int n, Counter c)
    {
        c.Count++;
        return n > 0;
    }

    private static HigherOrderShortCircuit Descriptor(string op, int n) =>
        op switch
        {
            "All" => HigherOrderShortCircuit.All,
            "Any" => HigherOrderShortCircuit.Any,
            "None" => HigherOrderShortCircuit.None,
            "AtLeast" => HigherOrderShortCircuit.AtLeast(n),
            "AtMost" => HigherOrderShortCircuit.AtMost(n),
            "Exactly" => HigherOrderShortCircuit.Exactly(n),
            _ => throw new ArgumentOutOfRangeException(nameof(op))
        };

    [Theory]
    [InlineData("All", 0, new[] { 1, 2, 3 }, true)]
    [InlineData("All", 0, new[] { 1, -2, 3 }, false)]
    [InlineData("All", 0, new int[0], true)]
    [InlineData("Any", 0, new[] { -1, -2, 3 }, true)]
    [InlineData("Any", 0, new[] { -1, -2, -3 }, false)]
    [InlineData("Any", 0, new int[0], false)]
    [InlineData("None", 0, new[] { -1, -2 }, true)]
    [InlineData("None", 0, new[] { -1, 2 }, false)]
    [InlineData("None", 0, new int[0], true)]
    [InlineData("AtLeast", 2, new[] { 1, 2, -3 }, true)]
    [InlineData("AtLeast", 2, new[] { 1, -2, -3 }, false)]
    [InlineData("AtLeast", 0, new int[0], true)]
    [InlineData("AtMost", 1, new[] { 1, -2, -3 }, true)]
    [InlineData("AtMost", 1, new[] { 1, 2, -3 }, false)]
    [InlineData("AtMost", 0, new int[0], true)]
    [InlineData("Exactly", 2, new[] { 1, 2, -3 }, true)]
    [InlineData("Exactly", 2, new[] { 1, 2, 3 }, false)]
    [InlineData("Exactly", 0, new int[0], true)]
    [InlineData("AtLeast", 3, new[] { 1, 2, 3 }, true)]
    [InlineData("AtLeast", 3, new[] { 1, 2, -3 }, false)]
    [InlineData("AtMost", 3, new[] { 1, 2, 3 }, true)]
    [InlineData("Exactly", 3, new[] { 1, 2, 3 }, true)]
    [InlineData("Exactly", 3, new[] { 1, 2, -3 }, false)]
    public void Evaluate_over_array_matches_reference(string op, int n, int[] data, bool expected)
    {
        Descriptor(op, n).Evaluate(data, new Counter(), Positive).ShouldBe(expected);
    }

    [Theory]
    [InlineData("All", new[] { 1, -2, 3 }, false)]   // IReadOnlyList<T> fast path
    [InlineData("Any", new[] { -1, 2, -3 }, true)]
    public void Evaluate_over_list_matches_reference(string op, int[] data, bool expected)
    {
        Descriptor(op, 0).Evaluate(new List<int>(data), new Counter(), Positive).ShouldBe(expected);
    }

    [Theory]
    [InlineData("All", new[] { 1, -2, 3 }, false)]   // non-array, non-IReadOnlyList fallback
    [InlineData("Any", new[] { -1, 2, -3 }, true)]
    public void Evaluate_over_lazy_enumerable_matches_reference(string op, int[] data, bool expected)
    {
        IEnumerable<int> lazy = data.Where(static _ => true);
        Descriptor(op, 0).Evaluate(lazy, new Counter(), Positive).ShouldBe(expected);
    }

    [Fact]
    public void All_short_circuits_on_first_false()
    {
        var counter = new Counter();
        HigherOrderShortCircuit.All.Evaluate(new[] { -1, 2, 3, 4 }, counter, Positive).ShouldBeFalse();
        counter.Count.ShouldBe(1); // stopped at the first element
    }

    [Fact]
    public void Any_short_circuits_on_first_true()
    {
        var counter = new Counter();
        HigherOrderShortCircuit.Any.Evaluate(new[] { 1, -2, -3, -4 }, counter, Positive).ShouldBeTrue();
        counter.Count.ShouldBe(1);
    }

    [Fact]
    public void AtLeast_short_circuits_once_threshold_reached()
    {
        var counter = new Counter();
        HigherOrderShortCircuit.AtLeast(2).Evaluate(new[] { 1, 2, 3, 4, 5 }, counter, Positive).ShouldBeTrue();
        counter.Count.ShouldBe(2);
    }

    [Fact]
    public void None_short_circuits_on_first_true()
    {
        var counter = new Counter();
        HigherOrderShortCircuit.None.Evaluate(new[] { 1, -2, -3, -4 }, counter, Positive).ShouldBeFalse();
        counter.Count.ShouldBe(1);
    }

    [Fact]
    public void AtMost_short_circuits_once_exceeded()
    {
        var counter = new Counter();
        HigherOrderShortCircuit.AtMost(1).Evaluate(new[] { 1, 2, 3, 4 }, counter, Positive).ShouldBeFalse();
        counter.Count.ShouldBe(2); // stops when the 2nd satisfied element pushes the count past 1
    }

    [Fact]
    public void Exactly_short_circuits_once_exceeded()
    {
        var counter = new Counter();
        HigherOrderShortCircuit.Exactly(1).Evaluate(new[] { 1, 2, 3, 4 }, counter, Positive).ShouldBeFalse();
        counter.Count.ShouldBe(2);
    }
}
