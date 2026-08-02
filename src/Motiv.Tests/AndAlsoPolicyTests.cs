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
}
