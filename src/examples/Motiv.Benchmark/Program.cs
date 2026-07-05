using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.dotTrace;
using BenchmarkDotNet.Running;
using Motiv.Benchmark;

_ = BenchmarkSwitcher.FromAssembly(typeof(MotivBenchmark).Assembly).Run(args);

namespace Motiv.Benchmark
{
    [DotTraceDiagnoser]
    [InProcess]
    public class MotivBenchmark
    {
        private readonly SpecBase<int, string> _isPositiveFromExpression;

        public MotivBenchmark()
        {
            _isPositiveFromExpression = Spec
                .From((int n) => n > 0)
                .WhenTrue("is positive")
                .WhenFalse("is not positive")
                .Create("is positive");
        }

        [Benchmark]
        public string[] EvaluateIsPositiveFromExpression()
        {
            return _isPositiveFromExpression.Evaluate(Random.Shared.Next(1, 21)).Assertions.ToArray();
        }

    }
}
