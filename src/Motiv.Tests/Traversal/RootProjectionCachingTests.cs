using Motiv.Traversal;

namespace Motiv.Tests.Traversal;

/// <summary>
/// Cover for ticket #193 — <see cref="BooleanResultBase{TMetadata}.RootValues" /> was the one root
/// projection with no cache, so every read re-walked the tier tree that #189 had just made
/// load-bearing. Its two siblings, <see cref="BooleanResultBase.RootAssertions" /> and
/// <see cref="BooleanResultBase.AllRootAssertions" />, have always answered a repeat read from an
/// array they materialised on the first.
/// </summary>
/// <remarks>
/// <para>
/// The defect had two halves, because the projection was eager and lazy in the wrong places. The fold
/// ran in the property getter, so a caller who read the property and never enumerated it still paid
/// for the whole walk; the de-duplication was an iterator, so a caller who enumerated one read twice
/// paid for that twice. The two cases below are those two halves, and neither is fixed by fixing the
/// other.
/// </para>
/// <para>
/// The cost is a census of <see cref="object.GetHashCode" /> calls, as in
/// <c>MetadataTierMaterialisationTests</c> next door — CI runs Windows and a clock there is a flake
/// rather than a bound. It undercounts: the fold's own memo hashes <see cref="MetadataNode{TMetadata}" />
/// references, which no metadatum can observe, so what these cases actually see is the de-duplication
/// pass riding on top of the walk. That is enough to discriminate a re-walk from a cached answer —
/// zero is zero — but it is not a measure of what the re-walk costs. The reference-identity case is
/// what states the whole of it.
/// </para>
/// </remarks>
public class RootProjectionCachingTests
{
    private const int Operands = 300;

    /// <summary>
    /// The ticket's own statement of the defect: "a consumer reading <c>RootValues</c> twice pays that
    /// twice". The chain is warmed first, so every tier and every metadata set is already built and
    /// what is left to count is only the work the second read repeats.
    /// </summary>
    [Theory]
    [InlineData("read the property twice")]
    [InlineData("enumerate one read twice")]
    public void Should_answer_a_repeat_read_of_RootValues_without_working_again(string repetition)
    {
        var result = CountedChain(Operands);
        var firstRead = result.RootValues;
        firstRead.ToArray();

        var hashes = CountingMetadata.CountHashes(() =>
        {
            var repeated = repetition switch
            {
                "read the property twice" => result.RootValues,
                "enumerate one read twice" => firstRead,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(repetition),
                    repetition,
                    "not one of the two halves of the defect")
            };

            repeated.ToArray();
        });

        hashes.ShouldBe(
            0,
            $"the answer was materialised by the first read, so a consumer who goes on to " +
            $"{repetition} should be handed it rather than charged for it again");
    }

    /// <summary>
    /// The ticket's title, as a symmetry across the three members it compares. Two of these rows have
    /// always passed; the third is the ticket. Reference identity is the only total way to say "this
    /// did not walk again" — the walk allocates nothing a metadatum can count, and a value comparison
    /// cannot tell a cached array from a freshly folded one that agrees with it.
    /// </summary>
    [Theory]
    [InlineData(nameof(BooleanResultBase.RootAssertions))]
    [InlineData(nameof(BooleanResultBase.AllRootAssertions))]
    [InlineData(nameof(BooleanResultBase<string>.RootValues))]
    public void Should_answer_every_root_projection_from_one_materialised_array(string projection)
    {
        var result = NamedChain(Operands);

        Read(result, projection).ShouldBeSameAs(
            Read(result, projection),
            $"{projection} is a projection of a result that cannot change once evaluated, so the " +
            $"second read should be the first read's array rather than a second walk of the tree");
    }

    /// <summary>
    /// The contract the cache must not buy its speed with. <c>RootValues</c> falls back to
    /// <see cref="BooleanResultBase{TMetadata}.Values" /> when the walk yields nothing, and
    /// <c>UnderlyingSourcesFallbackTests.Should_leave_the_root_value_projections_falling_back_to_their_own_values</c>
    /// is why that fallback exists rather than being swept away with #188's. Caching the projection
    /// caches the fallback with it, so the fallback needs stating on the second read as well as the
    /// first.
    /// </summary>
    [Fact]
    public void Should_still_fall_back_to_its_own_values_on_a_repeat_read()
    {
        var leaf = OracleHelpers.Leaf("a", true).Evaluate("model");

        leaf.RootValues.ShouldBe(["a-true"]);
        leaf.RootValues.ShouldBe(["a-true"], "a cached fallback is still the fallback");
    }

    /// <summary>
    /// The other contract a cache could quietly break: the answer itself. A left-deep <c>And</c> chain
    /// of distinctly-valued operands is fully causal, so its root values are every operand's value in
    /// chain order — a claim about content and order that must survive being answered from an array.
    /// </summary>
    [Fact]
    public void Should_answer_a_repeat_read_with_the_same_values_in_the_same_order()
    {
        var result = NamedChain(Operands);
        var expected = Enumerable.Range(0, Operands).Select(i => $"p{i} is even == true").ToArray();

        result.RootValues.ShouldBe(expected);
        result.RootValues.ShouldBe(expected, "a repeat read answers the same question");
    }

    private static BooleanResultBase<string> NamedChain(int operands) =>
        Enumerable
            .Range(0, operands)
            .Select(i => (SpecBase<int, string>)Spec.Build((int n) => n % 2 == 0).Create($"p{i} is even"))
            .Aggregate((left, right) => left.And(right))
            .Evaluate(2);

    private static BooleanResultBase<CountingMetadata> CountedChain(int operands) =>
        Enumerable
            .Range(0, operands)
            .Select(i => (SpecBase<int, CountingMetadata>)Spec
                .Build((int n) => n % 2 == 0)
                .WhenTrue(new CountingMetadata(i))
                .WhenFalse(new CountingMetadata(-i - 1))
                .Create($"p{i} is even"))
            .Aggregate((left, right) => left.And(right))
            .Evaluate(2);

    private static IEnumerable<string> Read(BooleanResultBase<string> result, string projection) =>
        projection switch
        {
            nameof(BooleanResultBase.RootAssertions) => result.RootAssertions,
            nameof(BooleanResultBase.AllRootAssertions) => result.AllRootAssertions,
            nameof(BooleanResultBase<string>.RootValues) => result.RootValues,
            _ => throw new ArgumentOutOfRangeException(
                nameof(projection),
                projection,
                "not one of the root projections")
        };

    /// <summary>
    /// A metadatum that counts the hashes taken of it. Private to this class for the same reason its
    /// twin in <c>MetadataTierMaterialisationTests</c> is: the count is static, xUnit does not run a
    /// class's cases in parallel with each other, and sharing the type between classes would make the
    /// counts race.
    /// </summary>
    private sealed class CountingMetadata(int id)
    {
        private static int _hashes;

        private int Id { get; } = id;

        public static int CountHashes(Action read)
        {
            var before = _hashes;
            read();
            return _hashes - before;
        }

        public override int GetHashCode()
        {
            _hashes++;
            return Id;
        }

        public override bool Equals(object? obj) => obj is CountingMetadata other && other.Id == Id;

        public override string ToString() => $"m{Id}";
    }
}
