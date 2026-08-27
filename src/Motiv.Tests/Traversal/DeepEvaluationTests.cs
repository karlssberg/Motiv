namespace Motiv.Tests.Traversal;

/// <summary>
/// Regression cover for the ceiling Spec 3A left standing. Evaluation walked the <em>spec</em> tree with
/// non-tail recursion — <c>AndSpec.EvaluateSpec</c> → <c>left.EvaluateInternal</c> → the next one — so a
/// composition deeper than the thread's stack aborted the process before any result member could be read.
/// </summary>
/// <remarks>
/// Each case runs on an explicitly-sized 1 MB thread, as <see cref="DeepCompositionTests" /> does: the
/// measured ceilings were 12,786 operands for <c>Evaluate</c>, 21,494 for <c>Matches</c> and — far the
/// worst, because a state-machine frame is far fatter — <b>633</b> for <c>EvaluateAsync</c>. On the 8 MB
/// main stack these would pass without the fix and prove nothing.
/// </remarks>
public class DeepEvaluationTests
{
    /// <summary>Comfortably past both measured ceilings, and past twice the higher one.</summary>
    private const int Operands = 50_000;

    private const int StackBytes = 1024 * 1024;

    [Fact]
    public void Should_evaluate_a_deep_And_chain() =>
        OnASmallStack(() => Chain((left, right) => left.And(right)).Evaluate(2).Satisfied.ShouldBeTrue());

    [Fact]
    public void Should_evaluate_a_deep_Or_chain() =>
        OnASmallStack(() => Chain((left, right) => left.Or(right)).Evaluate(2).Satisfied.ShouldBeTrue());

    [Fact]
    public void Should_evaluate_a_deep_XOr_chain() =>
        OnASmallStack(() => _ = Chain((left, right) => left.XOr(right)).Evaluate(2).Satisfied);

    [Fact]
    public void Should_evaluate_a_deep_AndAlso_chain() =>
        OnASmallStack(() => Chain((left, right) => left.AndAlso(right)).Evaluate(2).Satisfied.ShouldBeTrue());

    [Fact]
    public void Should_evaluate_a_deep_OrElse_chain() =>
        OnASmallStack(() => Chain((left, right) => left.OrElse(right)).Evaluate(2).Satisfied.ShouldBeTrue());

    /// <summary>
    /// Negation nests rather than chains, so the depth comes from wrapping rather than from folding —
    /// the shape a <c>!(!(!(…)))</c> document binds to.
    /// </summary>
    [Fact]
    public void Should_evaluate_a_deep_Not_nest() =>
        OnASmallStack(() => Nest().Evaluate(2).Satisfied.ShouldBeTrue());

    /// <summary>
    /// A policy chain, which composes through the policy-preserving operators and so folds
    /// <see cref="PolicyResultBase{TMetadata}" /> rather than <see cref="BooleanResultBase{TMetadata}" />.
    /// </summary>
    [Fact]
    public void Should_evaluate_a_deep_policy_chain() =>
        OnASmallStack(() => PolicyChain().Evaluate(2).Value.ShouldNotBeNull());

    [Fact]
    public void Should_match_a_deep_And_chain() =>
        OnASmallStack(() => Chain((left, right) => left.And(right)).Matches(2).ShouldBeTrue());

    [Fact]
    public void Should_match_a_deep_AndAlso_chain() =>
        OnASmallStack(() => Chain((left, right) => left.AndAlso(right)).Matches(2).ShouldBeTrue());

    [Fact]
    public void Should_match_a_deep_Not_nest() =>
        OnASmallStack(() => Nest().Matches(2).ShouldBeTrue());

    /// <summary>
    /// Async evaluation had the lowest ceiling of the three by a factor of twenty, and is what a rule
    /// document composes when it names an async spec.
    /// </summary>
    [Fact]
    public void Should_evaluate_a_deep_async_And_chain() =>
        OnASmallStack(() => AsyncChain((left, right) => left.And(right))
            .EvaluateAsync(2).AsTask().GetAwaiter().GetResult().Satisfied.ShouldBeTrue());

    [Fact]
    public void Should_evaluate_a_deep_async_AndAlso_chain() =>
        OnASmallStack(() => AsyncChain((left, right) => left.AndAlso(right))
            .EvaluateAsync(2).AsTask().GetAwaiter().GetResult().Satisfied.ShouldBeTrue());

    [Fact]
    public void Should_match_a_deep_async_And_chain() =>
        OnASmallStack(() => AsyncChain((left, right) => left.And(right))
            .MatchesAsync(2).AsTask().GetAwaiter().GetResult().ShouldBeTrue());

    /// <summary>
    /// The uniformity invariant carried down to evaluation: reading every member of a result composed
    /// past both ceilings, on one small stack. Spec 3A proved this for a 3,000-operand composition,
    /// which evaluation could still reach.
    /// </summary>
    [Fact]
    public void Should_read_every_member_of_a_deeply_evaluated_result() =>
        OnASmallStack(() =>
        {
            var result = Chain((left, right) => left.OrElse(right)).Evaluate(2);

            _ = result.Satisfied;
            _ = result.Reason;
            _ = result.Justification;
            _ = result.Assertions.Count();
            _ = result.AllAssertions.Count();
            _ = result.RootAssertions.Count();
            _ = result.Values.Count();
            _ = result.RootValues.Count();
            _ = result.Description.Reason;
        });

    private static SpecBase<int, string> Chain(
        Func<SpecBase<int, string>, SpecBase<int, string>, SpecBase<int, string>> combine) =>
        Operand().Take(Operands).Aggregate(combine);

    private static AsyncSpecBase<int, string> AsyncChain(
        Func<AsyncSpecBase<int, string>, AsyncSpecBase<int, string>, AsyncSpecBase<int, string>> combine) =>
        Operand().Take(Operands).Select(spec => spec.ToAsyncSpec()).Aggregate(combine);

    private static PolicyBase<int, string> PolicyChain() =>
        Enumerable
            .Range(0, Operands)
            .Select(i => Spec.Build((int n) => n % 2 == 0).Create($"p{i} is even"))
            .Aggregate((PolicyBase<int, string> left, PolicyBase<int, string> right) => left.OrElse(right));

    /// <summary>An even number of negations, so the outcome is the operand's own.</summary>
    private static SpecBase<int, string> Nest()
    {
        var spec = Operand().First();

        for (var i = 0; i < Operands; i++)
            spec = spec.Not();

        return spec;
    }

    private static IEnumerable<SpecBase<int, string>> Operand() =>
        Enumerable
            .Range(0, int.MaxValue)
            .Select(i => (SpecBase<int, string>)Spec.Build((int n) => n % 2 == 0).Create($"p{i} is even"));

    private static void OnASmallStack(Action body)
    {
        Exception? failure = null;

        var thread = new Thread(
            () =>
            {
                try
                {
                    body();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            },
            StackBytes);

        thread.Start();
        thread.Join();

        if (failure is not null)
            throw failure;
    }
}
