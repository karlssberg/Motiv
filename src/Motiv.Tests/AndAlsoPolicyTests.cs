namespace Motiv.Tests;

public class AndAlsoPolicyTests
{
    private static PolicyBase<string, string> Gate(bool satisfied, string name) =>
        Spec.Build<string>(_ => satisfied)
            .WhenTrue($"{name}-true")
            .WhenFalse($"{name}-false")
            .Create(name);

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Should_render_a_conjunction_of_two_policies_identically_before_and_after_policy_preservation(
        bool leftSatisfied,
        bool rightSatisfied)
    {
        // Arrange
        var composed = Gate(leftSatisfied, "left").AndAlso(Gate(rightSatisfied, "right"));

        // Act
        var result = composed.Evaluate("model");

        // Assert — these renderings are the public contract for every existing consumer who
        // combined two policies. Making AndAlso policy-preserving must not change any of them.
        result.Satisfied.ShouldBe(leftSatisfied && rightSatisfied);
        result.Reason.ShouldBe(ExpectedReason(leftSatisfied, rightSatisfied));
        result.Assertions.ShouldBe(ExpectedAssertions(leftSatisfied, rightSatisfied));
        result.Values.ShouldBe(ExpectedValues(leftSatisfied, rightSatisfied));
    }

    private static string ExpectedReason(bool left, bool right) =>
        (left, right) switch
        {
            (true, true) => "(left == true) && (right == true)",
            (true, false) => "right == false",
            (false, true) => "left == false",
            (false, false) => "left == false"
        };

    private static string[] ExpectedAssertions(bool left, bool right) =>
        (left, right) switch
        {
            (true, true) => ["left == true", "right == true"],
            (true, false) => ["right == false"],
            (false, true) => ["left == false"],
            (false, false) => ["left == false"]
        };

    private static string[] ExpectedValues(bool left, bool right) =>
        (left, right) switch
        {
            (true, true) => ["left-true", "right-true"],
            (true, false) => ["right-false"],
            (false, true) => ["left-false"],
            (false, false) => ["left-false"]
        };

    private static PolicyResultBase<string> Evaluated(bool satisfied, string name) =>
        Gate(satisfied, name).Evaluate("model");

    [Fact]
    public void Should_select_the_first_failure_when_combining_two_policy_results()
    {
        // Arrange — the left gate passes, so the right is the decisive one.
        var left = Evaluated(true, "left");
        var right = Evaluated(false, "right");

        // Act
        var result = left.AndAlso(right);

        // Assert
        result.Satisfied.ShouldBeFalse();

        // Value is the last-evaluated operand: for a conjunction that means the gate that failed.
        result.Value.ShouldBe("right-false");

        // Only the failing gate is causal — a passing gate did not cause an unsatisfied conjunction.
        result.Values.ShouldBe(["right-false"]);
    }

    [Fact]
    public void Should_short_circuit_on_an_unsatisfied_left_policy_result()
    {
        // Arrange
        var left = Evaluated(false, "left");
        var right = Evaluated(true, "right");

        // Act — the left already decided the outcome, so the right is not part of the result.
        var result = left.AndAlso(right);

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Value.ShouldBe("left-false");
        result.Values.ShouldBe(["left-false"]);
        result.Underlying.Count().ShouldBe(1);
    }

    [Fact]
    public void Should_select_the_last_evaluated_value_when_every_policy_result_is_satisfied()
    {
        // Arrange
        var left = Evaluated(true, "left");
        var right = Evaluated(true, "right");

        // Act
        var result = left.AndAlso(right);

        // Assert
        result.Satisfied.ShouldBeTrue();

        // All gates passed, so no operand is decisive; Value takes the last evaluated, and
        // Values still reports every contributing cause.
        result.Value.ShouldBe("right-true");
        result.Values.ShouldBe(["left-true", "right-true"]);
    }

    [Fact]
    public void Should_preserve_the_policy_when_combining_two_propositions()
    {
        // Arrange
        var composed = Gate(true, "left").AndAlso(Gate(false, "right"));

        // Act
        var result = composed.Evaluate("model");

        // Assert — the static type is the point: AndAlso on two policies yields a policy,
        // so `.Value` is available without a cast.
        result.Value.ShouldBe("right-false");
        result.Values.ShouldBe(["right-false"]);
        result.Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void Should_not_evaluate_the_right_proposition_when_the_left_is_unsatisfied()
    {
        // Arrange
        var rightEvaluations = 0;
        var left = Gate(false, "left");
        var right = Spec
            .Build<string>(_ => { rightEvaluations++; return true; })
            .WhenTrue("right-true")
            .WhenFalse("right-false")
            .Create("right");

        // Act
        var result = left.AndAlso(right).Evaluate("model");

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Value.ShouldBe("left-false");
        rightEvaluations.ShouldBe(0);
    }

    private static AsyncPolicyBase<string, string> AsyncGate(bool satisfied, string name) =>
        Spec.BuildAsync<string>(_ => new ValueTask<bool>(satisfied))
            .WhenTrue($"{name}-true")
            .WhenFalse($"{name}-false")
            .Create(name);

    [Fact]
    public async Task Should_preserve_the_policy_when_combining_two_async_propositions()
    {
        // Arrange
        var composed = AsyncGate(true, "left").AndAlso(AsyncGate(false, "right"));

        // Act
        var result = await composed.EvaluateAsync("model");

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Value.ShouldBe("right-false");
        result.Values.ShouldBe(["right-false"]);
    }

    [Fact]
    public async Task Should_never_start_the_right_async_operand_when_the_left_is_unsatisfied()
    {
        // Arrange — the whole point of async short-circuiting: the right operand's I/O never begins.
        var rightStarted = false;
        var left = AsyncGate(false, "left");
        var right = Spec
            .BuildAsync<string>(_ => { rightStarted = true; return new ValueTask<bool>(true); })
            .WhenTrue("right-true")
            .WhenFalse("right-false")
            .Create("right");

        // Act
        var result = await left.AndAlso(right).EvaluateAsync("model");

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Value.ShouldBe("left-false");
        rightStarted.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_lift_a_sync_policy_into_an_async_conjunction()
    {
        // Arrange
        var composed = AsyncGate(true, "left").AndAlso(Gate(false, "right"));

        // Act
        var result = await composed.EvaluateAsync("model");

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Value.ShouldBe("right-false");
    }

    private static ExpressionPolicyBase<int, string> ExprGate(int threshold, string name) =>
        Spec.From((int n) => n > threshold)
            .WhenTrue($"{name}-true")
            .WhenFalse($"{name}-false")
            .Create(name);

    [Fact]
    public void Should_preserve_the_policy_when_combining_two_expression_propositions()
    {
        // Arrange — 5 > 0 is satisfied, 5 > 10 is not, so the right gate is decisive.
        var composed = ExprGate(0, "above-zero").AndAlso(ExprGate(10, "above-ten"));

        // Act
        var result = composed.Evaluate(5);

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Value.ShouldBe("above-ten-false");
    }

    [Fact]
    public void Should_preserve_the_policy_when_mixing_expression_and_plain_propositions()
    {
        // Arrange
        var composed = ExprGate(0, "above-zero").AndAlso(
            Spec.Build<int>(_ => false).WhenTrue("plain-true").WhenFalse("plain-false").Create("plain"));

        // Act
        var result = composed.Evaluate(5);

        // Assert — degrading from ExpressionPolicyBase to PolicyBase is fine; degrading to a
        // spec is not, so `.Value` must still be reachable.
        result.Satisfied.ShouldBeFalse();
        result.Value.ShouldBe("plain-false");
    }

    [Fact]
    public void Should_select_the_first_failing_gate_in_a_chain()
    {
        // Arrange
        var policies = new[] { Gate(true, "a"), Gate(false, "b"), Gate(false, "c") };

        // Act
        var result = policies.AndAlsoTogether().Evaluate("model");

        // Assert — "b" fails first, so "c" is never evaluated and "b" is the value.
        result.Satisfied.ShouldBeFalse();
        result.Value.ShouldBe("b-false");
        result.Values.ShouldBe(["b-false"]);
    }

    [Fact]
    public void Should_flatten_every_cause_of_a_fully_satisfied_chain()
    {
        // Arrange
        var policies = new[] { Gate(true, "a"), Gate(true, "b"), Gate(true, "c") };

        // Act
        var result = policies.AndAlsoTogether().Evaluate("model");

        // Assert
        result.Satisfied.ShouldBeTrue();

        // No gate is decisive when all pass, so Value takes the last evaluated — but Values
        // flattens the left-nested tree and reports all three.
        result.Value.ShouldBe("c-true");
        result.Values.ShouldBe(["a-true", "b-true", "c-true"]);
    }

    [Fact]
    public void Should_combine_policy_results_with_AndAlsoTogether()
    {
        // Arrange
        var results = new[] { Evaluated(true, "a"), Evaluated(false, "b"), Evaluated(true, "c") };

        // Act
        var combined = results.AndAlsoTogether();

        // Assert
        combined.Satisfied.ShouldBeFalse();
        combined.Value.ShouldBe("b-false");
    }
}
