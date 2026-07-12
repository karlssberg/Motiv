using BenchmarkDotNet.Attributes;

namespace Motiv.Benchmark;

/// <summary>
/// Allocation-focused benchmarks for the proposition evaluation hot paths. Each benchmark pair contrasts
/// the boolean-only cost (reading <c>Satisfied</c>) with the explanation cost (reading <c>Reason</c> or
/// <c>Assertions</c>), so per-evaluation allocations show up independently of lazy explanation costs.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class EvaluationBenchmarks
{
    public record Message(string Text);

    private readonly PolicyBase<int, string> _minimal =
        Spec.Build((int n) => n % 2 == 0)
            .Create("is even");

    private readonly PolicyBase<int, string> _explanation =
        Spec.Build((int n) => n > 0)
            .WhenTrue("is positive")
            .WhenFalse("is not positive")
            .Create();

    private readonly PolicyBase<int, Message> _metadata =
        Spec.Build((int n) => n > 0)
            .WhenTrue(new Message("is positive"))
            .WhenFalse(new Message("is not positive"))
            .Create("is positive");

    private readonly SpecBase<int, string> _multiAssertion =
        Spec.Build((int n) => n > 0)
            .WhenTrueYield(_ => ["is positive", "is not negative"])
            .WhenFalseYield(_ => ["is not positive"])
            .Create("is positive");

    private readonly SpecBase<int, string> _composed;

    private readonly SpecBase<IEnumerable<int>, string> _allPositive =
        Spec.Build((int n) => n > 0)
            .AsAllSatisfied()
            .Create("all are positive");

    private readonly SpecBase<IEnumerable<int>, string> _allPositiveFromSpec =
        Spec.Build(
                Spec.Build((int n) => n > 0)
                    .WhenTrue("is positive")
                    .WhenFalse("is not positive")
                    .Create())
            .AsAllSatisfied()
            .Create("all are positive");

    private readonly SpecBase<IEnumerable<int>, string> _allPositiveFromExpression =
        Spec.From((int n) => n > 0)
            .AsAllSatisfied()
            .Create("all are positive");

    private readonly int[] _models = [1, -2, 3, -4, 5, 6, -7, 8, 9, 10];

    public EvaluationBenchmarks()
    {
        var isPositive = Spec.Build((int n) => n > 0)
            .WhenTrue("is positive")
            .WhenFalse("is not positive")
            .Create();

        var isEven = Spec.Build((int n) => n % 2 == 0)
            .WhenTrue("is even")
            .WhenFalse("is odd")
            .Create();

        _composed = isPositive & isEven;
    }

    [Benchmark]
    public bool Minimal_Satisfied() => _minimal.Evaluate(7).Satisfied;

    [Benchmark]
    public bool Minimal_Matches() => _minimal.Matches(7);

    [Benchmark]
    public string Minimal_Reason() => _minimal.Evaluate(7).Reason;

    [Benchmark]
    public bool Explanation_Satisfied() => _explanation.Evaluate(7).Satisfied;

    [Benchmark]
    public bool Explanation_Matches() => _explanation.Matches(7);

    [Benchmark]
    public string Explanation_Reason() => _explanation.Evaluate(7).Reason;

    [Benchmark]
    public bool Metadata_Satisfied() => _metadata.Evaluate(7).Satisfied;

    [Benchmark]
    public bool Metadata_Matches() => _metadata.Matches(7);

    [Benchmark]
    public Message Metadata_Value() => _metadata.Evaluate(7).Value;

    [Benchmark]
    public bool MultiAssertion_Satisfied() => _multiAssertion.Evaluate(7).Satisfied;

    [Benchmark]
    public bool MultiAssertion_Matches() => _multiAssertion.Matches(7);

    [Benchmark]
    public int MultiAssertion_Assertions() => _multiAssertion.Evaluate(7).Assertions.Count();

    [Benchmark]
    public bool Composed_Satisfied() => _composed.Evaluate(6).Satisfied;

    [Benchmark]
    public bool Composed_Matches() => _composed.Matches(6);

    [Benchmark]
    public string Composed_Reason() => _composed.Evaluate(6).Reason;

    [Benchmark]
    public bool HigherOrder_Satisfied() => _allPositive.Evaluate(_models).Satisfied;

    [Benchmark]
    public bool HigherOrder_Matches() => _allPositive.Matches(_models);

    [Benchmark]
    public int HigherOrder_Assertions() => _allPositive.Evaluate(_models).Assertions.Count();

    [Benchmark]
    public bool HigherOrderFromSpec_Satisfied() => _allPositiveFromSpec.Evaluate(_models).Satisfied;

    [Benchmark]
    public bool HigherOrderFromSpec_Matches() => _allPositiveFromSpec.Matches(_models);

    [Benchmark]
    public bool HigherOrderFromExpression_Satisfied() => _allPositiveFromExpression.Evaluate(_models).Satisfied;

    [Benchmark]
    public bool HigherOrderFromExpression_Matches() => _allPositiveFromExpression.Matches(_models);
}
