using Motiv.BooleanPredicateProposition;

namespace Motiv.Tests;

public class ExplanationBooleanResultTests
{
    public class ResultDescription(string reason, string statement) : ResultDescriptionBase
    {
        public override string Reason => reason;
        internal override int CausalOperandCount { get; } = 1;
        internal override string Statement => statement;
        public override IEnumerable<string> GetJustificationAsLines() => [reason];
    }

    private enum MyMetadata
    {
        True,
        False
    }

    [Theory]
    [InlineAutoData]
    public void Should_support_explicit_conversion_to_a_bool(
        bool isSatisfied,
        string because,
        ResultDescription resultDescription)
    {
        // Arrange
        var result = new PropositionBooleanResult<string>(
            isSatisfied,
            new Lazy<MetadataNode<string>>(() => new MetadataNode<string>(because, [])),
            new Lazy<Explanation>(() => new Explanation(because, [])),
            new Lazy<ResultDescriptionBase>(() => resultDescription));

        // Act
        var act = (bool)result;

        // Assert
        act.Should().Be(isSatisfied);
    }

    [Theory]
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, false)]
    [InlineAutoData(true, false, false)]
    [InlineAutoData(true, true, true)]
    public void Should_support_and_operation(bool left, bool right, bool expected)
    {
        // Arrange
        var leftResult = new PropositionBooleanResult<string>(
            left,
            new Lazy<MetadataNode<string>>(() => new MetadataNode<string>(left.ToString(), [])),
            new Lazy<Explanation>(() => new Explanation(left.ToString(), [])),
            new Lazy<ResultDescriptionBase>(() =>
                new BooleanResultDescription(left.ToString(), left.ToString())));

        var rightResult = new  PropositionBooleanResult<string>(
            right,
            new Lazy<MetadataNode<string>>(() => new MetadataNode<string>(right.ToString(), [])),
            new Lazy<Explanation>(() => new Explanation(right.ToString(), [])),
            new Lazy<ResultDescriptionBase>(() =>
                new BooleanResultDescription(right.ToString(), right.ToString())));

        var result = leftResult & rightResult;

        // Act
        var act = result.Satisfied;

        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(true, false)]
    [InlineAutoData(true, true)]
    public void Should_provide_assertions_when_two_results_have_the_and_operation_applied_to_them(bool left, bool right)
    {
        // Arrange
        var leftResult = new PropositionBooleanResult<string>(
            left,
            new Lazy<MetadataNode<string>>(() => new MetadataNode<string>(left.ToString(), [])),
            new Lazy<Explanation>(() => new Explanation(left.ToString(), [])),
            new Lazy<ResultDescriptionBase>(() =>
                new BooleanResultDescription(left.ToString(), left.ToString())));

        var rightResult = new  PropositionBooleanResult<string>(
            right,
            new Lazy<MetadataNode<string>>(() => new MetadataNode<string>(right.ToString(), [])),
            new Lazy<Explanation>(() => new Explanation(right.ToString(), [])),
            new Lazy<ResultDescriptionBase>(() =>
                new BooleanResultDescription(right.ToString(), right.ToString())));

        var result = leftResult & rightResult;

        var operands = new[] { leftResult, rightResult }
            .Where(operand => operand == result)
            .ToList();

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().Contain(operands.GetAssertions());
    }


    [Theory]
    [InlineAutoData(false, false, "False")]
    [InlineAutoData(false, true, "False")]
    [InlineAutoData(true, false, "False")]
    [InlineAutoData(true, true, "True")]
    public void Should_assert_and_operation_with_incompatible_metadata(bool left, bool right, params string[] expected)
    {
        // Arrange
        var leftResult = new PropositionBooleanResult<bool>(
            left,
            new Lazy<MetadataNode<bool>>(() => new MetadataNode<bool>(left, [])),
            new Lazy<Explanation>(() => new Explanation(left.ToString(), [])),
            new Lazy<ResultDescriptionBase>(() =>
                new BooleanResultDescription(left.ToString(), left.ToString())));
        var rightResult = new  PropositionBooleanResult<MyMetadata>(
            right,
            new Lazy<MetadataNode<MyMetadata>>(() => new MetadataNode<MyMetadata>(right ? MyMetadata.True : MyMetadata.False, [])),
            new Lazy<Explanation>(() => new Explanation(right.ToString(), [])),
            new Lazy<ResultDescriptionBase>(() =>
                new BooleanResultDescription(right.ToString(), right.ToString())));

        var result = leftResult & rightResult;

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().Contain(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(true, true, true)]
    public void Should_support_or_operation(bool left, bool right, bool expected)
    {
        // Arrange
        var leftResult = new PropositionBooleanResult<string>(
            left,
            new Lazy<MetadataNode<string>>(() => new MetadataNode<string>(left.ToString(), [])),
            new Lazy<Explanation>(() => new Explanation(left.ToString(), [])),
            new Lazy<ResultDescriptionBase>(() =>
                new BooleanResultDescription(left.ToString(), left.ToString())));

        var rightResult = new  PropositionBooleanResult<string>(
            right,
            new Lazy<MetadataNode<string>>(() => new MetadataNode<string>(right.ToString(), [])),
            new Lazy<Explanation>(() => new Explanation(right.ToString(), [])),
            new Lazy<ResultDescriptionBase>(() =>
                new BooleanResultDescription(right.ToString(), right.ToString())));

        var result = leftResult | rightResult;

        // Act
        var act = result.Satisfied;

        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(true, false)]
    [InlineAutoData(true, true)]
    public void Should_assert_or_operation(bool left, bool right)
    {
        // Arrange
        var leftResult = new PropositionBooleanResult<string>(
            left,
            new Lazy<MetadataNode<string>>(() => new MetadataNode<string>(left.ToString(), [])),
            new Lazy<Explanation>(() => new Explanation(left.ToString(), [])),
            new Lazy<ResultDescriptionBase>(() =>
                new BooleanResultDescription(left.ToString(), left.ToString())));

        var rightResult = new  PropositionBooleanResult<string>(
            right,
            new Lazy<MetadataNode<string>>(() => new MetadataNode<string>(right.ToString(), [])),
            new Lazy<Explanation>(() => new Explanation(right.ToString(), [])),
            new Lazy<ResultDescriptionBase>(() =>
                new BooleanResultDescription(right.ToString(), right.ToString())));

        var result = leftResult | rightResult;

        var operands = new[] { leftResult, rightResult }
            .Where(operand => operand == result)
            .ToList();

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().Contain(operands.SelectMany(operand => operand.Explanation.Assertions));
    }

    [Theory]
    [InlineAutoData(false, false, "False")]
    [InlineAutoData(false, true, "True")]
    [InlineAutoData(true, false, "True")]
    [InlineAutoData(true, true, "True")]
    public void Should_assert_or_operation_with_incompatible_metadata(bool left, bool right, params string[] expected)
    {
        // Arrange
        var leftResult = new PropositionBooleanResult<bool>(
            left,
            new Lazy<MetadataNode<bool>>(() => new MetadataNode<bool>(left, [])),
            new Lazy<Explanation>(() => new Explanation(left.ToString(), [])),
            new Lazy<ResultDescriptionBase>(() =>
                new BooleanResultDescription(left.ToString(), left.ToString())));
        var rightResult = new  PropositionBooleanResult<MyMetadata>(
            right,
            new Lazy<MetadataNode<MyMetadata>>(() => new MetadataNode<MyMetadata>(right ? MyMetadata.True : MyMetadata.False, [])),
            new Lazy<Explanation>(() => new Explanation(right.ToString(), [])),
            new Lazy<ResultDescriptionBase>(() =>
                new BooleanResultDescription(right.ToString(), right.ToString())));

        var result = leftResult | rightResult;

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().Contain(expected);
    }

    [Theory]
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(true, true, false)]
    public void Should_support_xor_operation(bool left, bool right, bool expected)
    {
        // Arrange
        var leftResult = new PropositionBooleanResult<string>(
            left,
            new Lazy<MetadataNode<string>>(() => new MetadataNode<string>(left.ToString(), [])),
            new Lazy<Explanation>(() => new Explanation(left.ToString(), [])),
            new Lazy<ResultDescriptionBase>(() =>
                new BooleanResultDescription(left.ToString(), left.ToString())));

        var rightResult = new  PropositionBooleanResult<string>(
            right,
            new Lazy<MetadataNode<string>>(() => new MetadataNode<string>(right.ToString(), [])),
            new Lazy<Explanation>(() => new Explanation(right.ToString(), [])),
            new Lazy<ResultDescriptionBase>(() =>
                new BooleanResultDescription(right.ToString(), right.ToString())));

        var result = leftResult ^ rightResult;

        // Act
        var act = result.Satisfied;

        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(true, false)]
    [InlineAutoData(true, true)]
    public void Should_yield_assertions_for_xor_operation(bool left, bool right)
    {
        // Arrange
        var leftResult = new PropositionBooleanResult<string>(
            left,
            new Lazy<MetadataNode<string>>(() => new MetadataNode<string>(left.ToString(), [])),
            new Lazy<Explanation>(() => new Explanation(left.ToString(), [])),
            new Lazy<ResultDescriptionBase>(() =>
                new BooleanResultDescription(left.ToString(), left.ToString())));

        var rightResult = new  PropositionBooleanResult<string>(
            right,
            new Lazy<MetadataNode<string>>(() => new MetadataNode<string>(right.ToString(), [])),
            new Lazy<Explanation>(() => new Explanation(right.ToString(), [])),
            new Lazy<ResultDescriptionBase>(() =>
                new BooleanResultDescription(right.ToString(), right.ToString())));

        var result = leftResult ^ rightResult;

        var operands = new[] { leftResult, rightResult };

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().Contain(operands.SelectMany(operand => operand.Explanation.Assertions));
    }

    [Theory]
    [InlineAutoData(false, false, "False")]
    [InlineAutoData(false, true, "False", "True")]
    [InlineAutoData(true, false, "True", "False")]
    [InlineAutoData(true, true, "True")]
    public void Should_assert_xor_operation_with_incompatible_metadata(bool left, bool right, params string[] expected)
    {
        // Arrange
        var leftResult = new PropositionBooleanResult<bool>(
            left,
            new Lazy<MetadataNode<bool>>(() => new MetadataNode<bool>(left, [])),
            new Lazy<Explanation>(() => new Explanation(left.ToString(), [])),
            new Lazy<ResultDescriptionBase>(() =>
                new BooleanResultDescription(left.ToString(), left.ToString())));

        var rightResult = new  PropositionBooleanResult<MyMetadata>(
            right,
            new Lazy<MetadataNode<MyMetadata>>(() => new MetadataNode<MyMetadata>(right ? MyMetadata.True : MyMetadata.False, [])),
            new Lazy<Explanation>(() => new Explanation(right.ToString(), [])),
            new Lazy<ResultDescriptionBase>(() =>
                new BooleanResultDescription(right.ToString(), right.ToString())));

        var result = leftResult ^ rightResult;

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().Contain(expected);
    }

    [Theory]
    [InlineAutoData(false, true)]
    [InlineAutoData(true, false)]
    public void Should_support_not_operation(bool operand, bool expected)
    {
        // Arrange
        var operandResult = new  PropositionBooleanResult<string>(
            operand,
            new Lazy<MetadataNode<string>>(() => new MetadataNode<string>(operand.ToString(), [])),
            new Lazy<Explanation>(() => new Explanation(operand.ToString(), [])),
            new Lazy<ResultDescriptionBase>(() =>
                new BooleanResultDescription(operand.ToString(), operand.ToString())));

        var result = !operandResult;

        // Act
        var act = result.Satisfied;

        // Assert
        act.Should().Be(expected);
    }

    [Theory]
    [InlineAutoData(false)]
    [InlineAutoData(true)]
    public void Should_yield_assertions_for_not_operation(bool operand)
    {
        // Arrange
        var operandResult = new  PropositionBooleanResult<string>(
            operand,
            new Lazy<MetadataNode<string>>(() => new MetadataNode<string>(operand.ToString(), [])),
            new Lazy<Explanation>(() => new Explanation(operand.ToString(), [])),
            new Lazy<ResultDescriptionBase>(() =>
                new BooleanResultDescription(operand.ToString(), operand.ToString())));

        var result = !operandResult;

        // Act
        var act = result.Assertions;

        // Assert
        act.Should().Contain(operandResult.Explanation.Assertions);
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

        var result = spec.IsSatisfiedBy(model);

        // Act
        var act = result.SubAssertions;

        // Assert
        act.Should().BeEquivalentTo(expected);
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
            .IsSatisfiedBy(leftSatisfied);

        var rightResult= Spec
            .Build((bool m) => m)
            .Create("right")
            .IsSatisfiedBy(rightSatisfied);

        // Act
        var act = leftResult.Equals(rightResult);

        // Assert
        act.Should().Be(expected);
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
            .IsSatisfiedBy(leftSatisfied);

        var rightResult= Spec
            .Build((bool m) => m)
            .WhenTrue(true)
            .WhenFalse(false)
            .Create("right")
            .IsSatisfiedBy(rightSatisfied);

        // Act
        var act = leftResult == rightResult;

        // Assert
        act.Should().Be(expected);
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
            .IsSatisfiedBy(satisfied);

        // Act
        var act = result.ToExplanationResult().Reason;

        // Assert
        act.Should().Be(satisfied.ToString());
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
            .IsSatisfiedBy(satisfied);

        // Act
        var act = result.ToExplanationResult().Description.ToString();

        // Assert
        act.Should().Be(satisfied.ToString());
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
            .IsSatisfiedBy(satisfied);

        // Act
        var act = result.ToExplanationResult().Justification;

        // Assert
        act.Should().Be(satisfied.ToString());
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
            .IsSatisfiedBy(satisfied);

        // Act
        var act = result.ToExplanationResult().Underlying.First().Reason;

        // Assert
        act.Should().Be(satisfied.ToString());
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
            .IsSatisfiedBy(satisfied);

        // Act
        var act = result.ToExplanationResult().UnderlyingWithMetadata.First().Reason;

        // Assert
        act.Should().Be(satisfied.ToString());
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
            .IsSatisfiedBy(satisfied);

        // Act
        var act = result.ToExplanationResult().Values;

        // Assert
        act.Should().BeEquivalentTo(satisfied.ToString().ToEnumerable());
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
            .IsSatisfiedBy(satisfied);

        // Act
        var act = result.ToExplanationResult().Causes.First().Reason;

        // Assert
        act.Should().Be(satisfied.ToString());
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
            .IsSatisfiedBy(satisfied);

        // Act
        var act = result.ToExplanationResult().CausesWithMetadata.First().Reason;

        // Assert
        act.Should().Be(satisfied.ToString());
    }
}
