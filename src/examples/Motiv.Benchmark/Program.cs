using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.dotTrace;
using BenchmarkDotNet.Running;
using Motiv.Benchmark;

var summary = BenchmarkRunner.Run<MotivBenchmark>();

namespace Motiv.Benchmark
{
    [DotTraceDiagnoser]
    [InProcess]
    public class MotivBenchmark
    {
        private readonly PolicyBase<int, string> _isPositive;
        private readonly SpecBase<int, string> _isPositiveFromExpression;

        public MotivBenchmark()
        {
            _isPositive = Spec
                .Build((int n) => n > 0)
                .Create("is positive");

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
