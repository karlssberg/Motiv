namespace Motiv.Tests.Traversal;

/// <summary>
/// Produces random result trees over the full node vocabulary, for the oracle differential suite.
/// Seeded, so a failing case is reproducible from its seed alone.
/// </summary>
internal static class ResultTreeGenerator
{
    /// <summary>The size of the corpus every suite that walks it shares.</summary>
    internal const int SeedCount = 150;

    /// <summary>
    /// The seeds as theory data, so a failure names the one case that reproduces it rather than
    /// collapsing the whole corpus into a single assertion.
    /// </summary>
    public static TheoryData<int> SeedData
    {
        get
        {
            var data = new TheoryData<int>();
            for (var seed = 1; seed <= SeedCount; seed++)
                data.Add(seed);
            return data;
        }
    }

    internal static IEnumerable<BooleanResultBase<string>> Corpus(int seed)
    {
        var rng = new Random(seed);
        var spec = seed % 5 == 0
            ? Spine(rng, 20)
            : BuildSpec(rng, rng.Next(2, 8));

        foreach (var model in new[] { 1, 2, 3, 4, 6, 12 })
            yield return spec.Evaluate(model);
    }

    /// <summary>Walks every node of a result tree, so the oracle is applied at every position.</summary>
    internal static IEnumerable<BooleanResultBase<string>> Nodes(BooleanResultBase<string> root)
    {
        var pending = new Stack<BooleanResultBase<string>>();
        var seen = new HashSet<BooleanResultBase<string>>(ReferenceComparer<BooleanResultBase<string>>.Instance);
        pending.Push(root);

        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (!seen.Add(node))
                continue;

            yield return node;

            foreach (var child in node.UnderlyingWithValues)
                pending.Push(child);
        }
    }

    /// <summary>
    /// Every node of every tree a seed generates — the unit almost every suite in this folder actually
    /// works in, rather than the roots.
    /// </summary>
    /// <remarks>
    /// Order matters and is exactly <see cref="Corpus" /> then <see cref="Nodes" />, root by root:
    /// <see cref="DescriptionBaselineTests" /> hashes its rendering in traversal order, so a helper
    /// that reordered or de-duplicated across roots would leave every other suite green and fail only
    /// that baseline, pointing at the formatters rather than at here.
    /// </remarks>
    internal static IEnumerable<BooleanResultBase<string>> CorpusNodes(int seed) =>
        Corpus(seed).SelectMany(Nodes);

    private static SpecBase<int, string> BuildSpec(Random rng, int depth)
    {
        if (depth <= 0)
            return Leaf(rng);

        var left = BuildSpec(rng, rng.Next(depth));
        var right = BuildSpec(rng, rng.Next(depth));

        return rng.Next(7) switch
        {
            0 => left.And(right),
            1 => left.Or(right),
            2 => left.XOr(right),
            3 => left.AndAlso(right),
            4 => left.OrElse(right),
            5 => left.Not(),
            _ => HigherOrder(rng, left)
        };
    }

    /// <summary>
    /// A left-deep chain — the shape that folds into a spine of binary results and is what actually
    /// overflows in the wild (<c>specs.Aggregate((a, b) => a.And(b))</c>).
    /// </summary>
    private static SpecBase<int, string> Spine(Random rng, int length)
    {
        var spine = Leaf(rng);

        for (var i = 1; i < length; i++)
        {
            var next = Leaf(rng);
            spine = rng.Next(5) switch
            {
                0 => spine.And(next),
                1 => spine.Or(next),
                2 => spine.XOr(next),
                3 => spine.AndAlso(next),
                _ => spine.OrElse(next)
            };
        }

        return spine;
    }

    private static SpecBase<int, string> Leaf(Random rng)
    {
        var k = rng.Next(1, 6);

        return rng.Next(4) switch
        {
            0 => Spec.Build((int n) => n % k == 0).Create($"divisible by {k}"),
            1 => Spec
                .Build((int n) => n > k)
                .WhenTrue($"greater than {k}")
                .WhenFalse($"not greater than {k}")
                .Create(),
            2 => Spec
                .Build((int n) => n < k)
                .WhenTrue($"less than {k}")
                .WhenFalse($"not less than {k}")
                .Create($"under {k}"),
            _ => Spec.From((int n) => n != k).Create($"not {k}")
        };
    }

    private static SpecBase<int, string> HigherOrder(Random rng, SpecBase<int, string> underlying)
    {
        var higherOrder = rng.Next(3) switch
        {
            0 => Spec.Build(underlying).AsAllSatisfied().Create("all neighbours"),
            1 => Spec.Build(underlying).AsAnySatisfied().Create("any neighbour"),
            _ => Spec.Build(underlying).AsAtLeastNSatisfied(2).Create("two neighbours")
        };

        return higherOrder.ChangeModelTo<int>(n => [n, n + 1, n + 2]);
    }
}

internal sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
{
    internal static readonly ReferenceComparer<T> Instance = new();

    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

    public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
