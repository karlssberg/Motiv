using System.Diagnostics;
using Motiv.Traversal;

namespace Motiv.Tests.Traversal;

/// <summary>
/// Cover for ticket #195 — the quadratic that was left when #136 and #137 finished with
/// <see cref="BooleanResultBase{TMetadata}.RootValues" />. Reading the <i>root</i> of a fully-causal
/// chain built every level's metadata set on the way, so an answer of <c>n</c> values cost
/// Θ(n²) <c>(level, metadatum)</c> memberships.
/// </summary>
/// <remarks>
/// <para>
/// Named for the materialisation rather than the cost because <c>MetadataTierCostTests</c> next door
/// already holds #137's — that file is about what the tier <i>unions</i>, this one about <i>when</i> it
/// is built.
/// </para>
/// <para>
/// The cost is stated as a count of <see cref="object.GetHashCode" /> calls rather than on a clock:
/// <see cref="HashSet{T}" /> hashes each item once as it is added, so the number of hashes taken to
/// answer one read is a census of how many set memberships that read built. At 300 operands the
/// defect reads 45,449 — <c>n(n+1)/2</c> plus the walk's own handful — for an answer of 300 items.
/// </para>
/// <para>
/// The laziness is the whole finding, so the second case is the other half of it: every level still
/// carries its own distinct set, and reading them all still costs what that structure costs. What
/// changed is that a caller who reads only the root no longer pays for the levels beneath it.
/// </para>
/// </remarks>
public class MetadataTierMaterialisationTests
{
    private const int Operands = 300;

    /// <summary>
    /// The cost, at the member the tier is read through. A chain of <c>n</c> distinctly-valued
    /// operands has an answer of <c>n</c> values, and reading it hashes each of them twice: once into
    /// the operand's own single-value set, once into the root's union. Building every intermediate
    /// level's set instead hashes each value once per level it appears in, which is the square.
    /// </summary>
    [Theory]
    [InlineData("And")]
    [InlineData("AndAlso")]
    [InlineData("OrElse")]
    public void Should_hash_each_value_twice_when_only_the_root_is_read(string combinator)
    {
        var result = Chain(combinator, Operands);

        var hashes = CountingMetadata.CountHashes(() => result.Values.ToArray());

        hashes.ShouldBe(
            Operands * 2,
            $"the root of the {combinator} chain answers with {Operands} values, each hashed into " +
            $"its operand's own set and then into the root's union; hashing each once per level it " +
            $"appears in costs {Operands * (Operands + 1) / 2}");
    }

    /// <summary>
    /// The cost at the member the ticket is named for. <see cref="BooleanResultBase{TMetadata}.RootValues" />
    /// is the tier walk on top of the read above, and its constant is neither interesting nor stable, so
    /// this case works in ratios instead. Two points cannot fit an exponent and it does not claim to:
    /// what it discriminates is doubling from quadrupling, at a threshold between the two.
    /// </summary>
    [Fact]
    public void Should_scale_linearly_with_the_chain_when_reading_RootValues()
    {
        var shortChain = Chain("And", Operands);
        var longChain = Chain("And", Operands * 2);

        var overShort = CountingMetadata.CountHashes(() => shortChain.RootValues.ToArray());
        var overLong = CountingMetadata.CountHashes(() => longChain.RootValues.ToArray());

        overLong.ShouldBeLessThanOrEqualTo(
            overShort * 5 / 2,
            $"twice the chain is twice the answer, so it should cost about twice the work — {overShort} " +
            $"over {Operands} operands and {overLong} over {Operands * 2}; a walk that materialises " +
            $"every level costs four times as much for twice the chain");
    }

    /// <summary>
    /// The contract the cost case must not buy its bound with. Level <i>k</i> of the chain is the
    /// composition of the first <i>k</i> operands, so it carries exactly those <i>k</i> values —
    /// a per-node answer that <see cref="MetadataNode{TMetadata}.Metadata" /> exposes publicly and
    /// that no change to when it is computed may flatten.
    /// </summary>
    [Fact]
    public void Should_still_carry_a_distinct_set_at_every_level_of_the_chain()
    {
        var spine = ChainSpine.Of(Chain("And", Operands)).ToArray();

        spine.Length.ShouldBe(Operands - 1, "a left-deep chain of n operands has n-1 compositions");

        for (var i = 0; i < spine.Length; i++)
            spine[i].MetadataTier.Metadata.Count().ShouldBe(
                Operands - i,
                "the composition of the first k operands of the chain carries exactly their k values");
    }

    /// <summary>
    /// What the bottom-up pass is for, once it is no longer forcing anything. Several tiers read their
    /// source as they are <i>constructed</i> — a decorator's is its underlying result's <c>Values</c> —
    /// so building them top-down nests two frames per level. Constructing them deepest-first is what
    /// keeps the deepest tier's construction a constant distance from the read that asked for it.
    /// </summary>
    /// <remarks>
    /// Stated as the stack depth at which the innermost tier is actually built, which is neither a
    /// clock nor a guess at how much stack a runner has: quadrupling the chain must not move it. With
    /// the pass removed it moves from roughly <c>2n</c> to <c>8n</c>. This shape has had no cover since
    /// the pass was introduced — <c>DeepCompositionTests</c> exercises composition chains, whose tiers
    /// are the unions this slice made iterative, and a decorator chain deep enough to overflow on their
    /// 1 MB thread exhausts it during evaluation first. It is the only case in the suite that fails
    /// when the pass is removed, which is the same thing as saying it was the gap.
    /// </remarks>
    [Fact]
    public void Should_build_the_deepest_tier_at_a_constant_stack_depth_however_long_the_chain()
    {
        var overShortChain = StackDepthOfDeepestTier(500);
        var overLongChain = StackDepthOfDeepestTier(2_000);

        overLongChain.ShouldBeLessThanOrEqualTo(
            overShortChain + 4,
            $"the innermost tier of a 2,000-deep chain of decorators was built {overLongChain} frames " +
            $"down and that of a 500-deep chain {overShortChain}; building the tiers deepest-first is " +
            $"what makes that a property of the read rather than of the chain");
    }

    /// <summary>
    /// The stack depth at which the innermost proposition's metadata is resolved when the root of a
    /// chain of <paramref name="decorators" /> decorators is read. The resolver runs as that tier is
    /// constructed, so it observes the descent that built it.
    /// </summary>
    private static int StackDepthOfDeepestTier(int decorators)
    {
        var depth = 0;

        SpecBase<int, string> spec = Spec
            .Build((int n) => n % 2 == 0)
            .WhenTrueYield(_ =>
            {
                depth = new StackTrace(false).FrameCount;
                return ["p0 is even"];
            })
            .WhenFalseYield(_ => ["p0 is not even"])
            .Create("p0");

        for (var i = 1; i < decorators; i++)
            spec = Spec.Build(spec).Create($"d{i}");

        spec.Evaluate(2).Values.ToArray();

        return depth;
    }

    /// <summary>
    /// A chain in which every operand is causal at every level, over each of the three places a
    /// composition's tier is built as the union of its causes'. <c>OrElse</c> is run against an
    /// unsatisfied model so that it, too, never short-circuits.
    /// </summary>
    private static BooleanResultBase<CountingMetadata> Chain(string combinator, int operands) =>
        Enumerable
            .Range(0, operands)
            .Select(i => (SpecBase<int, CountingMetadata>)Spec
                .Build((int n) => n % 2 == 0)
                .WhenTrue(new CountingMetadata(i))
                .WhenFalse(new CountingMetadata(-i - 1))
                .Create($"p{i} is even"))
            .Aggregate(Combine(combinator))
            .Evaluate(combinator == "OrElse" ? 3 : 2);

    private static Func<SpecBase<int, CountingMetadata>, SpecBase<int, CountingMetadata>,
        SpecBase<int, CountingMetadata>> Combine(string combinator) =>
        combinator switch
        {
            "And" => (left, right) => left.And(right),
            "AndAlso" => (left, right) => ((PolicyBase<int, CountingMetadata>)left)
                .AndAlso((PolicyBase<int, CountingMetadata>)right),
            "OrElse" => (left, right) => ((PolicyBase<int, CountingMetadata>)left)
                .OrElse((PolicyBase<int, CountingMetadata>)right),
            _ => throw new ArgumentOutOfRangeException(
                nameof(combinator),
                combinator,
                "not one of the combinators whose tier is the union of its causes'")
        };

    /// <summary>
    /// A metadatum that counts the hashes taken of it, so that the tier's cost can be asserted as a
    /// number rather than waited for on a clock — CI runs Windows, and a timing assertion there is a
    /// flake to be re-run rather than a bound to be read.
    /// </summary>
    /// <remarks>
    /// The count is static and therefore only safe while this type stays private to one test class:
    /// xUnit does not run a class's cases in parallel with each other, and nothing outside this file
    /// can reach it. Promoting it to a shared helper would make the counts race.
    /// </remarks>
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
