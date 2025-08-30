using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.dotTrace;
using BenchmarkDotNet.Running;
using Motiv.Benchmark;

_ = BenchmarkRunner.Run<MotivBenchmark>();

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
            return _isPositiveFromExpression.IsSatisfiedBy(Random.Shared.Next(1, 21)).Assertions.ToArray();
        }

    }
}
