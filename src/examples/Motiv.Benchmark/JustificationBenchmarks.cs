using BenchmarkDotNet.Attributes;

namespace Motiv.Benchmark;

/// <summary>
/// Allocation-focused benchmarks for the justification/serialization rendering paths. These exercise the
/// description tree walks that only occur when <c>Justification</c> (or an <c>Explanation</c> string) is read —
/// the costs the evaluation benchmarks intentionally exclude.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class JustificationBenchmarks
{
    private readonly SpecBase<IEnumerable<int>, string> _allPositive;

    private readonly SpecBase<int, string> _composed;

    private readonly SpecBase<int, string> _fromExpression =
        Spec.From((int n) => n > 0 & n % 2 == 0)
            .Create("is positive and even");

    private readonly int[] _wideModels = Enumerable.Range(-50, 100).ToArray();

    public JustificationBenchmarks()
    {
        var isPositiveExplained = Spec.Build((int n) => n > 0)
            .WhenTrue(n => $"{n} is positive")
            .WhenFalse(n => $"{n} is not positive")
            .Create("is positive");

        _allPositive = Spec.Build(isPositiveExplained)
            .AsAllSatisfied()
            .Create("all numbers are positive");

        var isPositive = Spec.Build((int n) => n > 0)
            .WhenTrue("is positive")
            .WhenFalse("is not positive")
            .Create();

        var isEven = Spec.Build((int n) => n % 2 == 0)
            .WhenTrue("is even")
            .WhenFalse("is odd")
            .Create();

        var isSmall = Spec.Build((int n) => n < 100)
            .WhenTrue("is small")
            .WhenFalse("is large")
            .Create();

        _composed = (isPositive & isEven) | !isSmall;
    }

    [Benchmark]
    public string HigherOrderWide_Justification() => _allPositive.Evaluate(_wideModels).Justification;

    [Benchmark]
    public string Composed_Justification() => _composed.Evaluate(6).Justification;

    [Benchmark]
    public string ExpressionTree_Justification() => _fromExpression.Evaluate(6).Justification;

    [Benchmark]
    public string ExpressionTree_Reason() => _fromExpression.Evaluate(6).Reason;

    [Benchmark]
    public string Explanation_ToString() => _composed.Evaluate(6).Explanation.ToString();
}
