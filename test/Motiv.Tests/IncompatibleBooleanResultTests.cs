using Motiv.Shared;
using Motiv.Tests.Customizations;

namespace Motiv.Tests;

public class IncompatibleBooleanResultTests
{
    private enum MyMetadata
    {
        True,
        False
    }

    /// <summary>
    /// Produces a string-metadata result whose single assertion is the outcome's <c>ToString()</c> ("True"/"False"),
    /// mirroring the hand-built results the deleted <c>PropositionBooleanResult</c> factory previously created.
    /// </summary>
    private static BooleanResultBase<string> StringResult(bool satisfied) =>
        Spec.Build((bool m) => m)
            .WhenTrue("True")
            .WhenFalse("False")
            .Create()
            .Evaluate(satisfied);

    /// <summary>
    /// Produces a result carrying an arbitrary (non-string) metadata type while asserting the outcome's
    /// <c>ToString()</c>, so that composing two such results exercises the incompatible-metadata fallback.
    /// </summary>
    private static BooleanResultBase<T> IncompatibleResult<T>(bool satisfied, T metadata) =>
        new PolicyResult<T>(
            satisfied,
            new MetadataNode<T>(metadata, []),
            new Explanation(satisfied.ToString()),
            new BooleanResultDescription(satisfied.ToString(), satisfied.ToString()),
            [],
            [],
            [],
            [],
            metadata);

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Should_support_explicit_conversion_to_a_bool(bool isSatisfied)
    {
        // Arrange
        var result = StringResult(isSatisfied);

        // Act
        var act = (bool)result;

        // Assert
        act.ShouldBe(isSatisfied);
    }

    [Theory]
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, false)]
    [InlineAutoData(true, false, false)]
    [InlineAutoData(true, true, true)]
    public void Should_support_and_operation(bool left, bool right, bool expected)
    {
        // Arrange
        var leftResult = StringResult(left);
        var rightResult = StringResult(right);

        var result = leftResult & rightResult;

        // Act
        var act = result.Satisfied;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(false, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(true, false)]
    [InlineAutoData(true, true)]
    public void Should_provide_assertions_when_two_results_have_the_and_operation_applied_to_them(bool left, bool right)
    {
        // Arrange
        var leftResult = StringResult(left);
        var rightResult = StringResult(right);

        var result = leftResult & rightResult;

        var operands = new[] { leftResult, rightResult }
            .Where(operand => operand == result)
            .ToList();

        // Act
        var act = result.Assertions;

        // Assert
        act.ShouldBeSubsetOf(operands.GetAssertions());
    }

    [Theory]
    [InlineAutoData(false, false, "False")]
    [InlineAutoData(false, true, "False")]
    [InlineAutoData(true, false, "False")]
    [InlineAutoData(true, true, "True")]
    public void Should_assert_with_incompatible_metadata(bool left, bool right, params string[] expected)
    {
        // Arrange
        var leftResult = IncompatibleResult(left, left);
        var rightResult = IncompatibleResult(right, right ? MyMetadata.True : MyMetadata.False);

        var result = leftResult & rightResult;

        // Act
        var act = result.Assertions;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(false, false, "False", "False")]
    [InlineAutoData(false, true, "False")]
    [InlineAutoData(true, false, "False")]
    [InlineAutoData(true, true, "True", "True")]
    public void Should_determine_causes_with_incompatible_metadata(bool left, bool right, params string[] expected)
    {
        // Arrange
        var leftResult = IncompatibleResult(left, left);
        var rightResult = IncompatibleResult(right, right ? MyMetadata.True : MyMetadata.False);

        var result = leftResult & rightResult;

        // Act
        var act = result.Causes.Select(cause => cause.Description.Statement);

        // Assert
        act.ShouldBe(expected, true);
    }

    [Theory]
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(true, true, true)]
    public void Should_support_or_operation(bool left, bool right, bool expected)
    {
        // Arrange
        var leftResult = StringResult(left);
        var rightResult = StringResult(right);

        var result = leftResult | rightResult;

        // Act
        var act = result.Satisfied;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(false, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(true, false)]
    [InlineAutoData(true, true)]
    public void Should_assert_or_operation(bool left, bool right)
    {
        // Arrange
        var leftResult = StringResult(left);
        var rightResult = StringResult(right);

        var result = leftResult | rightResult;

        var operands = new[] { leftResult, rightResult }
            .Where(operand => operand == result)
            .ToList();

        // Act
        var act = result.Assertions;

        // Assert
        act.ShouldBeSubsetOf(operands.SelectMany(operand => operand.Explanation.Assertions));
    }

    [Theory]
    [InlineAutoData(false, false, "False")]
    [InlineAutoData(false, true, "True")]
    [InlineAutoData(true, false, "True")]
    [InlineAutoData(true, true, "True")]
    public void Should_assert_or_operation_with_incompatible_metadata(bool left, bool right, params string[] expected)
    {
        // Arrange
        var leftResult = IncompatibleResult(left, left);
        var rightResult = IncompatibleResult(right, right ? MyMetadata.True : MyMetadata.False);

        var result = leftResult | rightResult;

        // Act
        var act = result.Assertions;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(true, true, false)]
    public void Should_support_xor_operation(bool left, bool right, bool expected)
    {
        // Arrange
        var leftResult = StringResult(left);
        var rightResult = StringResult(right);

        var result = leftResult ^ rightResult;

        // Act
        var act = result.Satisfied;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(false, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(true, false)]
    [InlineAutoData(true, true)]
    public void Should_yield_assertions_for_xor_operation(bool left, bool right)
    {
        // Arrange
        var leftResult = StringResult(left);
        var rightResult = StringResult(right);

        var result = leftResult ^ rightResult;

        var operands = new[] { leftResult, rightResult };

        // Act
        var act = result.Assertions;

        // Assert
        act.ShouldBeSubsetOf(operands.SelectMany(operand => operand.Explanation.Assertions));
    }

    [Theory]
    [InlineAutoData(false, false, "False")]
    [InlineAutoData(false, true, "False", "True")]
    [InlineAutoData(true, false, "True", "False")]
    [InlineAutoData(true, true, "True")]
    public void Should_assert_xor_operation_with_incompatible_metadata(bool left, bool right, params string[] expected)
    {
        // Arrange
        var leftResult = IncompatibleResult(left, left);
        var rightResult = IncompatibleResult(right, right ? MyMetadata.True : MyMetadata.False);

        var result = leftResult ^ rightResult;

        // Act
        var act = result.Assertions;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(false, true)]
    [InlineAutoData(true, false)]
    public void Should_support_not_operation(bool operand, bool expected)
    {
        // Arrange
        var operandResult = StringResult(operand);

        var result = !operandResult;

        // Act
        var act = result.Satisfied;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(false)]
    [InlineAutoData(true)]
    public void Should_yield_assertions_for_not_operation(bool operand)
    {
        // Arrange
        var operandResult = StringResult(operand);

        var result = !operandResult;

        // Act
        var act = result.Assertions;

        // Assert
        act.ShouldBeSubsetOf(operandResult.Explanation.Assertions);
    }

    [Theory]
    [InlineAutoData(true, "underlying is true")]
    [InlineAutoData(false, "underlying is false")]
    public void Should_generate_sub_assertions(bool model, string expected)
    {
        // Arrange
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue("underlying is true")
            .WhenFalse("underlying is false")
            .Create();

        var spec = Spec
            .Build(underlyingSpec)
            .WhenTrue("top-level true")
            .WhenFalse("top-level false")
            .Create("top-level proposition");

        var result = spec.Evaluate(model);

        // Act
        var act = result.SubAssertions;

        // Assert
        act.ShouldBe([expected]);
    }

    [Theory]
    [InlineAutoData(false, false, true)]
    [InlineAutoData(false, true, false)]
    [InlineAutoData(true, false, false)]
    [InlineAutoData(true, true, true)]
    public void Should_override_equals_method_to_compare_satisfied_property(
        bool leftSatisfied,
        bool rightSatisfied,
        bool expected)
    {
        // Arrange
        var leftResult = Spec
            .Build((bool m) => m)
            .Create("left")
            .Evaluate(leftSatisfied);

        var rightResult= Spec
            .Build((bool m) => m)
            .Create("right")
            .Evaluate(rightSatisfied);

        // Act
        var act = leftResult.Equals(rightResult);

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(false, false, true)]
    [InlineAutoData(false, true, false)]
    [InlineAutoData(true, false, false)]
    [InlineAutoData(true, true, true)]
    public void Should_override_equals_operator_to_compare_satisfied_property(
        bool leftSatisfied,
        bool rightSatisfied,
        bool expected)
    {
        // Arrange
        var leftResult = Spec
            .Build((bool m) => m)
            .Create("left")
            .Evaluate(leftSatisfied);

        var rightResult= Spec
            .Build((bool m) => m)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("right")
            .Evaluate(rightSatisfied);

        // Act
        var act = leftResult == rightResult;

        // Assert
        act.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(false)]
    [InlineAutoData(true)]
    public void Should_provide_reason_to_explanation_boolean_result(bool satisfied)
    {
        // Arrange
        var result = Spec
            .Build((bool m) => m)
            .WhenTrue(m => m.ToString())
            .WhenFalse(m => m.ToString())
            .Create("policy")
            .Evaluate(satisfied);

        // Act
        var act = result.ToExplanationResult().Reason;

        // Assert
        act.ShouldBe($"policy == {(satisfied ? "true" : "false")}");
    }

    [Theory]
    [InlineAutoData(false)]
    [InlineAutoData(true)]
    public void Should_provide_serialize_description_of_explanation_boolean_result(bool satisfied)
    {
        // Arrange
        var result = Spec
            .Build((bool m) => m)
            .WhenTrue(m => m.ToString())
            .WhenFalse(m => m.ToString())
            .Create("policy")
            .Evaluate(satisfied);

        // Act
        var act = result.ToExplanationResult().Description.ToString();

        // Assert
        act.ShouldBe($"policy == {(satisfied ? "true" : "false")}");
    }

    [Theory]
    [InlineAutoData(false)]
    [InlineAutoData(true)]
    public void Should_provide_justification_to_explanation_boolean_result(bool satisfied)
    {
        // Arrange
        var result = Spec
            .Build((bool m) => m)
            .WhenTrue(m => m.ToString())
            .WhenFalse(m => m.ToString())
            .Create("policy")
            .Evaluate(satisfied);

        // Act
        var act = result.ToExplanationResult().Justification;

        // Assert
        act.ShouldBe($"policy == {(satisfied ? "true" : "false")}");
    }

    [Theory]
    [InlineAutoData(false)]
    [InlineAutoData(true)]
    public void Should_provide_underlying_result_to_explanation_boolean_result(bool satisfied,
        string randomText)
    {
        // Arrange
        var underlying = Spec.Build((bool m) => m)
            .WhenTrue(m => m.ToString())
            .WhenFalse(m => m.ToString())
            .Create("underlying");

        var result = Spec
            .Build(underlying)
            .WhenTrue(randomText)
            .WhenFalse(randomText)
            .Create("policy")
            .Evaluate(satisfied);

        // Act
        var act = result.ToExplanationResult().Underlying.First().Reason;

        // Assert
        act.ShouldBe($"underlying == {(satisfied ? "true" : "false")}");
    }

    [Theory]
    [InlineAutoData(false)]
    [InlineAutoData(true)]
    public void Should_provide_underlying_with_metadata_result_to_explanation_boolean_result(bool satisfied,
        string randomText)
    {
        // Arrange
        var underlying = Spec.Build((bool m) => m)
            .WhenTrue(m => m.ToString())
            .WhenFalse(m => m.ToString())
            .Create("underlying");

        var result = Spec
            .Build(underlying)
            .WhenTrue(randomText)
            .WhenFalse(randomText)
            .Create("policy")
            .Evaluate(satisfied);

        // Act
        var act = result.ToExplanationResult().UnderlyingWithValues.First().Reason;

        // Assert
        act.ShouldBe($"underlying == {(satisfied ? "true" : "false")}");
    }

    [Theory]
    [InlineAutoData(false)]
    [InlineAutoData(true)]
    public void Should_provide_metadata_to_explanation_boolean_result(bool satisfied)
    {
        // Arrange
        var result = Spec
            .Build((bool m) => m)
            .WhenTrue(m => m.ToString())
            .WhenFalse(m => m.ToString())
            .Create("policy")
            .Evaluate(satisfied);

        // Act
        var act = result.ToExplanationResult().Values;

        // Assert
        act.ShouldBe($"policy == {(satisfied ? "true" : "false")}".ToEnumerable());
    }

    [Theory]
    [InlineAutoData(false)]
    [InlineAutoData(true)]
    public void Should_provide_causes_of_explanation_boolean_result(bool satisfied, string randomText)
    {
        // Arrange
        var underlying = Spec.Build((bool m) => m)
            .WhenTrue(m => m.ToString())
            .WhenFalse(m => m.ToString())
            .Create("underlying");

        var result = Spec
            .Build(underlying)
            .WhenTrue(randomText)
            .WhenFalse(randomText)
            .Create("policy")
            .Evaluate(satisfied);

        // Act
        var act = result.ToExplanationResult().Causes.First().Reason;

        // Assert
        act.ShouldBe($"underlying == {(satisfied ? "true" : "false")}");
    }

    [Theory]
    [InlineAutoData(false)]
    [InlineAutoData(true)]
    public void Should_provide_causes_with_metadata_of_explanation_boolean_result(bool satisfied, string randomText)
    {
        // Arrange
        var underlying = Spec.Build((bool m) => m)
            .WhenTrue(m => m.ToString())
            .WhenFalse(m => m.ToString())
            .Create("underlying");

        var result = Spec
            .Build(underlying)
            .WhenTrue(randomText)
            .WhenFalse(randomText)
            .Create("policy")
            .Evaluate(satisfied);

        // Act
        var act = result.ToExplanationResult().CausesWithValues.First().Reason;

        // Assert
        act.ShouldBe($"underlying == {(satisfied ? "true" : "false")}");
    }
}
