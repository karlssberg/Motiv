using Motiv.Serialization;

namespace Motiv.Serialization.Tests;

public class ResultSerializerTests
{
    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0)
            .WhenTrue("is positive")
            .WhenFalse("is not positive")
            .Create();

    private static SpecBase<int, string> IsEven { get; } =
        Spec.Build((int n) => n % 2 == 0)
            .WhenTrue("is even")
            .WhenFalse("is odd")
            .Create();

    [Fact]
    public void Should_project_scalar_fields_from_a_leaf_result()
    {
        // Arrange
        var result = IsPositive.Evaluate(5);

        // Act
        var dto = new ResultSerializer().ToEvaluationResult(result);

        // Assert
        dto.Satisfied.ShouldBe(result.Satisfied);
        dto.Reason.ShouldBe(result.Reason);
        dto.Assertions.ShouldBe(result.Assertions.ToArray());
        dto.Values.ShouldBe(result.Values.ToArray());
        dto.Justification.ShouldBe(result.Justification);
    }
}
