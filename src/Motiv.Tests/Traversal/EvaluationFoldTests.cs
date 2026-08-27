namespace Motiv.Tests.Traversal;

/// <summary>
/// The properties the evaluation fold could break without any existing assertion noticing. A result's
/// value says which operands <em>contributed</em>; it does not say which were <em>evaluated</em>, so
/// short-circuiting and operand order are asserted here by counting evaluations directly.
/// </summary>
public class EvaluationFoldTests
{
    [Fact]
    public void Should_not_evaluate_the_right_operand_of_an_AndAlso_whose_left_is_unsatisfied()
    {
        var right = Counting(_ => true);

        Never().AndAlso(right.Spec).Evaluate(0).Satisfied.ShouldBeFalse();

        right.Evaluations.ShouldBe(0);
    }

    [Fact]
    public void Should_not_evaluate_the_right_operand_of_an_OrElse_whose_left_is_satisfied()
    {
        var right = Counting(_ => true);

        Always().OrElse(right.Spec).Evaluate(0).Satisfied.ShouldBeTrue();

        right.Evaluations.ShouldBe(0);
    }

    [Fact]
    public void Should_not_match_the_right_operand_of_an_AndAlso_whose_left_is_unsatisfied()
    {
        var right = Counting(_ => true);

        Never().AndAlso(right.Spec).Matches(0).ShouldBeFalse();

        right.Evaluations.ShouldBe(0);
    }

    [Fact]
    public void Should_not_match_the_right_operand_of_an_OrElse_whose_left_is_satisfied()
    {
        var right = Counting(_ => true);

        Always().OrElse(right.Spec).Matches(0).ShouldBeTrue();

        right.Evaluations.ShouldBe(0);
    }

    [Fact]
    public void Should_evaluate_both_operands_of_an_eager_operator_even_when_the_left_settles_the_outcome()
    {
        var right = Counting(_ => true);

        Never().And(right.Spec).Evaluate(0).Satisfied.ShouldBeFalse();

        right.Evaluations.ShouldBe(1);
    }

    [Fact]
    public void Should_evaluate_the_left_operand_before_the_right()
    {
        var order = new List<string>();
        var left = Counting(_ => { order.Add("left"); return true; });
        var right = Counting(_ => { order.Add("right"); return true; });

        left.Spec.And(right.Spec).Evaluate(0);

        order.ShouldBe(["left", "right"]);
    }

    /// <summary>
    /// A left-deep chain evaluates every operand once and in source order. The fold pushes frames down
    /// the left spine and unwinds, so an order defect here would be invisible to any assertion that only
    /// reads the result.
    /// </summary>
    [Fact]
    public void Should_evaluate_every_operand_of_a_chain_exactly_once_and_in_order()
    {
        var order = new List<int>();
        var operands = Enumerable
            .Range(0, 10)
            .Select(i => Counting(_ => { order.Add(i); return true; }))
            .ToArray();

        operands
            .Select(operand => operand.Spec)
            .Aggregate((left, right) => left.And(right))
            .Evaluate(0);

        order.ShouldBe(Enumerable.Range(0, 10));
        operands.ShouldAllBe(operand => operand.Evaluations == 1);
    }

    /// <summary>
    /// A composition whose operands are decorated propositions rather than atomic ones — the fold calls
    /// through to the operand's own evaluation there, and the decoration must survive that.
    /// </summary>
    [Fact]
    public void Should_carry_the_assertions_of_decorated_operands_through_the_fold()
    {
        var left = Spec.Build((int n) => n > 0).WhenTrue("positive").WhenFalse("not positive").Create();
        var right = Spec.Build((int n) => n % 2 == 0).WhenTrue("even").WhenFalse("odd").Create();

        var result = left.And(right).Evaluate(2);

        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["positive", "even"]);
    }

    [Fact]
    public async Task Should_not_evaluate_the_right_operand_of_an_async_AndAlso_whose_left_is_unsatisfied()
    {
        var right = Counting(_ => true);

        var result = await Never().ToAsyncSpec().AndAlso(right.Spec.ToAsyncSpec()).EvaluateAsync(0);

        result.Satisfied.ShouldBeFalse();
        right.Evaluations.ShouldBe(0);
    }

    [Fact]
    public async Task Should_not_evaluate_the_right_operand_of_an_async_OrElse_whose_left_is_satisfied()
    {
        var right = Counting(_ => true);

        var result = await Always().ToAsyncSpec().OrElse(right.Spec.ToAsyncSpec()).EvaluateAsync(0);

        result.Satisfied.ShouldBeTrue();
        right.Evaluations.ShouldBe(0);
    }

    /// <summary>
    /// The concurrent operators are the one shape the async fold does not drive, so they still have to
    /// evaluate both operands and compose the same result.
    /// </summary>
    [Fact]
    public async Task Should_evaluate_both_operands_of_a_concurrent_async_operator()
    {
        var left = Counting(_ => true);
        var right = Counting(_ => false);

        var result = await left.Spec.ToAsyncSpec().AndConcurrently(right.Spec.ToAsyncSpec()).EvaluateAsync(0);

        result.Satisfied.ShouldBeFalse();
        left.Evaluations.ShouldBe(1);
        right.Evaluations.ShouldBe(1);
    }

    /// <summary>
    /// The token has to reach every operand, not just the first two. Threading it was previously a
    /// parameter passed down the recursion; the fold carries it in a local instead, which is the sort of
    /// change that quietly drops it at depth.
    /// </summary>
    [Fact]
    public async Task Should_thread_the_cancellation_token_to_every_operand_of_a_folded_chain()
    {
        using var cancellation = new CancellationTokenSource();
        var observed = new List<CancellationToken>();

        var chain = Enumerable
            .Range(0, 100)
            .Select(i => Spec
                .BuildAsync((int _, CancellationToken token) =>
                {
                    observed.Add(token);
                    return new ValueTask<bool>(true);
                })
                .Create($"p{i}"))
            .Aggregate((AsyncSpecBase<int, string> left, AsyncSpecBase<int, string> right) => left.And(right));

        await chain.EvaluateAsync(0, cancellation.Token);

        observed.Count.ShouldBe(100);
        observed.ShouldAllBe(token => token == cancellation.Token);
    }

    private static SpecBase<int, string> Always() =>
        Spec.Build((int _) => true).Create("always");

    private static SpecBase<int, string> Never() =>
        Spec.Build((int _) => false).Create("never");

    private static CountingSpec Counting(Func<int, bool> predicate) => new(predicate);

    private sealed class CountingSpec
    {
        public CountingSpec(Func<int, bool> predicate) =>
            Spec = Motiv.Spec
                .Build((int model) =>
                {
                    Evaluations++;
                    return predicate(model);
                })
                .Create("counted");

        public SpecBase<int, string> Spec { get; }

        public int Evaluations { get; private set; }
    }
}
