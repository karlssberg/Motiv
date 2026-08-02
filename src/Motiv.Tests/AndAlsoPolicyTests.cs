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
}
