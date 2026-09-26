using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace Motiv.Benchmark;

/// <summary>
/// Compares the ways a captured value inside a <c>Spec.From</c> expression tree can be resolved when
/// serializing it to C#: reflection over the closure field (what the construction-time serializer does
/// today), compiling the subexpression to a delegate (JIT or interpreted), and caching that delegate.
/// The mechanisms are reproduced here because the serializer is internal; the end-to-end benchmarks in
/// <see cref="SpecFromConstructionBenchmarks"/> measure the public surface.
/// </summary>
[MemoryDiagnoser]
public class ConstantResolutionBenchmarks
{
    public record Config(int Limit);

    private MemberExpression _capturedLocal = null!;
    private MemberExpression _capturedChain = null!;
    private ParameterExpression _parameter = null!;
    private Func<int, object> _cachedDelegate = null!;

    private static readonly ConditionalWeakTable<Expression, Func<int, object>> Cache = new();

    [GlobalSetup]
    public void Setup()
    {
        var threshold = 5;
        var config = new Config(5);
        Expression<Func<int, bool>> local = n => n == threshold;
        Expression<Func<int, bool>> chain = n => n == config.Limit;
        _capturedLocal = (MemberExpression)((BinaryExpression)local.Body).Right;
        _capturedChain = (MemberExpression)((BinaryExpression)chain.Body).Right;
        _parameter = local.Parameters[0];
        _cachedDelegate = CompileWithParameter(_capturedLocal);
        Cache.GetValue(_capturedLocal, CompileWithParameter);
    }

    private Func<int, object> CompileWithParameter(Expression node) =>
        Expression.Lambda<Func<int, object>>(Expression.Convert(node, typeof(object)), _parameter).Compile();

    // ---- one-shot: construction-time resolution of `threshold` ----

    [Benchmark(Baseline = true, Description = "Reflection, by name (today)")]
    public object? Reflection_ByName()
    {
        var closure = ((ConstantExpression)_capturedLocal.Expression!).Value!;
        var type = closure.GetType();
        if (!(type.Name.StartsWith("<>") || type.Name.Contains("DisplayClass"))) return closure;
        return type.GetField(_capturedLocal.Member.Name)!.GetValue(closure);
    }

    [Benchmark(Description = "Reflection, MemberInfo from node")]
    public object? Reflection_MemberInfo() => EvaluateByReflection(_capturedLocal);

    [Benchmark(Description = "Compile() then invoke")]
    public object? Compile_Jit() =>
        Expression.Lambda<Func<object>>(Expression.Convert(_capturedLocal, typeof(object))).Compile()();

    [Benchmark(Description = "Compile(preferInterpretation) then invoke")]
    public object? Compile_Interpreted() =>
        Expression.Lambda<Func<object>>(Expression.Convert(_capturedLocal, typeof(object))).Compile(preferInterpretation: true)();

    // ---- one-shot: member chain `config.Limit` (field then property) ----

    [Benchmark(Description = "Chain: reflection, MemberInfo from node")]
    public object? Chain_Reflection() => EvaluateByReflection(_capturedChain);

    [Benchmark(Description = "Chain: Compile() then invoke")]
    public object? Chain_Compile_Jit() =>
        Expression.Lambda<Func<object>>(Expression.Convert(_capturedChain, typeof(object))).Compile()();

    // ---- repeated: per-evaluation resolution (the generic serializer's path) ----

    [Benchmark(Description = "Per-eval: CWT lookup + cached delegate (today)")]
    public object PerEval_CachedInCwt() => Cache.GetValue(_capturedLocal, CompileWithParameter)(5);

    [Benchmark(Description = "Per-eval: delegate held on the proposition")]
    public object PerEval_HeldDelegate() => _cachedDelegate(5);

    [Benchmark(Description = "Per-eval: reflection, MemberInfo from node")]
    public object? PerEval_Reflection() => EvaluateByReflection(_capturedLocal);

    [Benchmark(Description = "Per-eval: compile every time (no cache)")]
    public object PerEval_Uncached() => CompileWithParameter(_capturedLocal)(5);

    /// <summary>The EF Core funcletizer's fast path: walk a member chain rooted in a constant (or a
    /// static member), reading each link with the FieldInfo/PropertyInfo the node already carries.</summary>
    internal static object? EvaluateByReflection(Expression node) =>
        node switch
        {
            ConstantExpression constant => constant.Value,
            MemberExpression { Member: FieldInfo field } member =>
                field.GetValue(member.Expression is null ? null : EvaluateByReflection(member.Expression)),
            MemberExpression { Member: PropertyInfo property } member =>
                property.GetValue(member.Expression is null ? null : EvaluateByReflection(member.Expression)),
            _ => throw new NotSupportedException(node.NodeType.ToString())
        };
}

/// <summary>End-to-end cost through the public API of the paths that resolve captured values.</summary>
[MemoryDiagnoser]
public class SpecFromConstructionBenchmarks
{
    private readonly int _threshold = 5;
    private readonly SpecBase<int, string> _isPositive = Spec.Build((int n) => n > 0).Create("is positive");
    private SpecBase<int, string> _asValueSpec = null!;

    [GlobalSetup]
    public void Setup() => _asValueSpec = CreateWithAsValue();

    [Benchmark(Description = "Construct Spec.From with AsValue(captured)")]
    public SpecBase<int, string> CreateWithAsValue()
    {
        var threshold = _threshold;
        return Spec.From((int n) => n >= Display.AsValue(threshold)).Create("at least threshold");
    }

    [Benchmark(Description = "Construct Spec.From over captured spec (.All)")]
    public SpecBase<int[], string> CreateWithCapturedSpec()
    {
        var isPositive = _isPositive;
        return Spec.From((int[] xs) => xs.All(isPositive)).Create("all positive");
    }

    [Benchmark(Description = "Evaluate + read Assertions, AsValue(captured)")]
    public int EvaluateAssertions() => _asValueSpec.Evaluate(7).Assertions.Count();
}
