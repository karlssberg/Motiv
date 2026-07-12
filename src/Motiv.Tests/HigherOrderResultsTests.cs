using Motiv.HigherOrderProposition;

namespace Motiv.Tests;

public class HigherOrderResultsTests
{
    // The projection receives the explicit state argument (here, a multiplier) so that
    // production call sites can pass a non-capturing static lambda.
    private static int[] Multiply(IEnumerable<int> source, int factor) =>
        HigherOrderResults.Materialize(source, factor, static (value, f) => value * f);

    [Fact]
    public void Should_materialize_a_read_only_list_source_via_the_pre_sized_indexed_path()
    {
        // Arrays implement IReadOnlyList, so this exercises the pre-sized indexed branch.
        int[] source = [1, 2, 3];

        Multiply(source, 10).ShouldBe([10, 20, 30]);
    }

    [Fact]
    public void Should_materialize_a_list_source_via_the_pre_sized_indexed_path()
    {
        List<int> source = [1, 2, 3];

        Multiply(source, 10).ShouldBe([10, 20, 30]);
    }

    [Fact]
    public void Should_materialize_a_lazy_source_via_the_buffered_fallback_path()
    {
        // A Select iterator is neither an array nor an IReadOnlyList, forcing the
        // buffered List fallback.
        var source = Enumerable.Range(1, 3).Select(value => value);

        Multiply(source, 10).ShouldBe([10, 20, 30]);
    }

    [Fact]
    public void Should_materialize_an_empty_source_to_an_empty_array()
    {
        Multiply([], 10).ShouldBeEmpty();
        Multiply(Enumerable.Empty<int>().Where(_ => true), 10).ShouldBeEmpty();
    }

    [Fact]
    public void Should_throw_argument_null_exception_for_a_null_source()
    {
        // Preserves the ArgumentNullException semantics of the prior source.Select(...).ToArray().
        Should.Throw<ArgumentNullException>(() => Multiply(null!, 10))
            .ParamName!.ShouldBe("source");
    }
}
