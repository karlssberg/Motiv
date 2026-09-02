using Motiv.Traversal;

namespace Motiv.Tests.Traversal;

/// <summary>
/// Cover for ticket #137 — the scale half of what Spec 3A measured and could not fix. Reading
/// <see cref="BooleanResultBase{TMetadata}.RootValues" /> over a fully-causal chain grew
/// super-quadratically — 3A filed it as roughly <c>n^2.6</c>, and re-measuring it for this slice
/// fitted <c>n^2.85</c> — rather than the <c>n^2</c> the tier's own shape costs; and over a chain of
/// <i>identically-named</i> propositions it exhausted memory at 300 operands.
/// </summary>
/// <remarks>
/// <para>
/// The ticket named <c>MetadataNode.Resolve</c>'s collapse comparison as the suspected driver, and —
/// as in <see href="https://github.com/karlssberg/Motiv/issues/189">#189</see> — that was where the
/// cost was paid rather than what caused it. Until ticket #136,
/// <see cref="BooleanResultBase{TMetadata}.UnderlyingMetadataSources" /> reported a composition as
/// its own source, so <c>Resolve</c> compared a node's metadata against a child set holding that node
/// and its operation-result descendants. Each of those carries O(k) metadata, which is the extra
/// factor; the collapse rule then descended into the self-edge, which is the out-of-memory.
/// </para>
/// <para>
/// #136 fixed both without measuring either, so nothing held them fixed. These cases do — every one
/// of them fails against the pre-#136 tree, the identically-named ones with an
/// <see cref="OutOfMemoryException" /> and the other two on their assertions.
/// </para>
/// </remarks>
public class MetadataTierCostTests
{
    private const int Operands = 300;

    /// <summary>
    /// The out-of-memory, over every combinator that leaves each operand of the chain causal. Every
    /// <i>operand</i> is satisfied, so the <c>AndAlso</c> chain never short-circuits and belongs here,
    /// while the <c>OrElse</c> chain always does — leaving one causal operand per level, which is the
    /// shape Spec 3A parked its <c>RootValues</c> regression on precisely to avoid this cost, and the
    /// only one of the five that survived the defect.
    /// </summary>
    /// <remarks>
    /// It is the operands rather than the compositions: the <c>XOr</c> chain itself alternates as it
    /// composes and is unsatisfied at the root, since 300 satisfied operands XOr to <c>false</c>.
    /// That changes nothing here — <c>XOr</c> is non-short-circuiting and always reports both
    /// operands, so every one of the 300 stays causal, which is the only property this case needs.
    /// </remarks>
    [Theory]
    [InlineData("And")]
    [InlineData("Or")]
    [InlineData("XOr")]
    [InlineData("AndAlso")]
    public void Should_read_RootValues_of_a_chain_of_identically_named_propositions(string combinator) =>
        Chain(combinator, _ => "is even").RootValues.ShouldBe(
            ["is even == true"],
            $"every operand of the {combinator} chain yields the same value, so the distinct set of " +
            $"root values is that one value — reached over a tier tree the size of the chain, not one " +
            $"that grows multiplicatively because every level collapses into the one below");

    /// <summary>
    /// The cost, in the only form a test can state without a clock. A source is an operand, so it
    /// carries the single value that operand yielded, and the metadata <c>Resolve</c> unions at the
    /// root is linear in the chain. While a composition was its own source the sources were operation
    /// results carrying O(k) values apiece and this sum was quadratic — the extra factor of <c>n</c>
    /// on top of what the tier itself costs. At 300 operands it read 45,151, which is
    /// <c>300 x 301 / 2 + 1</c>.
    /// </summary>
    [Fact]
    public void Should_reach_operands_carrying_one_value_each_rather_than_compositions()
    {
        var sources = Chain("And", i => $"p{i} is even").UnderlyingMetadataSources.ToArray();

        sources.Length.ShouldBe(Operands, "one source per operand");
        sources.Sum(source => source.Values.Count()).ShouldBe(
            Operands,
            "each source is an operand carrying its own single value, so the metadata the tier tree " +
            "unions is linear in the chain rather than quadratic in it");
    }

    /// <summary>
    /// The mechanism behind both, asserted directly so that a regression reports the cyclic edge
    /// rather than an out-of-memory that says nothing about where it came from.
    /// <c>UnderlyingMetadataSourcesTests.Should_yield_the_child_the_walk_stopped_at_rather_than_the_result_itself</c>
    /// makes the same claim over three operands; this one makes it at every level of a chain deep
    /// enough for the consequence to be fatal.
    /// </summary>
    [Fact]
    public void Should_not_report_any_composition_in_the_chain_as_its_own_metadata_source()
    {
        foreach (var composition in ChainSpine.Of(Chain("And", i => $"p{i} is even")))
            composition.UnderlyingMetadataSources.ShouldNotContain(
                source => ReferenceEquals(source, composition),
                "a node that is its own metadata source makes the tier tree cyclic — Resolve then " +
                "finds a child saying exactly what its parent says, collapses the level into itself, " +
                "and descends for ever");
    }

    private static BooleanResultBase<string> Chain(string combinator, Func<int, string> name) =>
        Enumerable
            .Range(0, Operands)
            .Select(i => (SpecBase<int, string>)Spec.Build((int n) => n % 2 == 0).Create(name(i)))
            .Aggregate(Combine(combinator))
            .Evaluate(2);

    private static Func<SpecBase<int, string>, SpecBase<int, string>, SpecBase<int, string>> Combine(
        string combinator) =>
        combinator switch
        {
            "And" => (left, right) => left.And(right),
            "Or" => (left, right) => left.Or(right),
            "XOr" => (left, right) => left.XOr(right),
            "AndAlso" => (left, right) => left.AndAlso(right),
            _ => throw new ArgumentOutOfRangeException(
                nameof(combinator),
                combinator,
                "not one of the combinators that leaves every operand causal")
        };
}
