using System.Text.Json;
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

    [Fact]
    public void Should_map_the_denoised_causal_explanation_tree()
    {
        // Arrange
        var result = IsPositive.And(IsEven).Evaluate(4);

        // Act
        var dto = new ResultSerializer().ToEvaluationResult(result);

        // Assert
        ShouldMirror(dto.Explanation, result.Explanation);
    }

    private static void ShouldMirror(ExplanationNode node, Motiv.Shared.Explanation explanation)
    {
        node.Assertions.ShouldBe(explanation.Assertions.ToArray());

        var underlying = explanation.Underlying.ToArray();
        node.Underlying.Count.ShouldBe(underlying.Length);
        for (var i = 0; i < underlying.Length; i++)
            ShouldMirror(node.Underlying[i], underlying[i]);
    }

    [Fact]
    public void Should_serialize_to_camelcase_json_with_the_expected_shape()
    {
        // Arrange
        var result = IsPositive.Evaluate(5);

        // Act
        var json = new ResultSerializer().Serialize(result);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("satisfied").GetBoolean().ShouldBeTrue();
        root.GetProperty("reason").GetString()!.ShouldBe("is positive");
        root.GetProperty("assertions")[0].GetString()!.ShouldBe("is positive");
        root.GetProperty("justification").GetString().ShouldNotBeNullOrWhiteSpace();
        root.GetProperty("explanation").GetProperty("assertions")[0].GetString()!.ShouldBe("is positive");
    }
}
