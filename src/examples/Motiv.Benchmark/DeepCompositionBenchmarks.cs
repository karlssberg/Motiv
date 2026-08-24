using BenchmarkDotNet.Attributes;

namespace Motiv.Benchmark;

/// <summary>
/// Allocation-focused benchmarks for the result-tree and description-tree walks over a deep
/// composition — the shape <c>specs.Aggregate((a, b) =&gt; a.And(b))</c> produces, which is what
/// Spec 3A made stack-safe.
/// </summary>
/// <remarks>
/// Ticket 19 predicted that replacing the memoised recursions with one iterative fold should lower
/// transient churn rather than raise it, conditional on a closure- and iterator-free inner loop.
/// These cases are what makes that claim measurable rather than asserted. The member with the most
/// to gain is <c>UnderlyingMetadataSources</c>, which re-walked its whole subtree on every access
/// before it was given the cache its two siblings already had.
/// </remarks>
[MemoryDiagnoser]
[ShortRunJob]
public class DeepCompositionBenchmarks
{
    [Params(50, 400)]
    public int Operands { get; set; }

    private SpecBase<int, string> _chain = null!;

    [GlobalSetup]
    public void Setup() =>
        _chain = Enumerable
            .Range(0, Operands)
            .Select(i => (SpecBase<int, string>)Spec.Build((int n) => n % 2 == 0).Create($"p{i} is even"))
            .Aggregate((left, right) => left.And(right));

    [Benchmark]
    public int Assertions() => _chain.Evaluate(2).Assertions.Count();

    [Benchmark]
    public int RootAssertions() => _chain.Evaluate(2).RootAssertions.Count();

    [Benchmark]
    public int UnderlyingMetadataSources() => _chain.Evaluate(2).UnderlyingMetadataSources.Count();

    [Benchmark]
    public string Justification() => _chain.Evaluate(2).Justification;

    [Benchmark]
    public string Reason() => _chain.Evaluate(2).Reason;
}
