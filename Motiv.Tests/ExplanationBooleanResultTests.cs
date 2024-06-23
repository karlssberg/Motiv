using Motiv.BooleanPredicateProposition;

namespace Motiv.Tests;

public class ExplanationBooleanResultTests
{
    [Theory]
    [InlineAutoData]
    public void Should_support_explicit_conversion_to_a_bool(
        bool isSatisfied,
        string because)
    {
        // Arrange
        var result = new PropositionBooleanResult<string>(
            isSatisfied, 
            new MetadataNode<string>(because, []), 
            new Explanation(because, []),
            because);

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
            new MetadataNode<string>(left.ToString(), []), 
            new Explanation(left.ToString(), []),
            left.ToString());
        
        var rightResult = new  PropositionBooleanResult<string>(
            right, 
            new MetadataNode<string>(right.ToString(), []), 
            new Explanation(right.ToString(), []),
            right.ToString());

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
            new MetadataNode<string>(left.ToString(), []), 
            new Explanation(left.ToString(), []),
            left.ToString());
        
        var rightResult = new  PropositionBooleanResult<string>(
            right, 
            new MetadataNode<string>(right.ToString(), []), 
            new Explanation(right.ToString(), []),
            right.ToString());

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
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(true, true, true)]
    public void Should_support_or_operation(bool left, bool right, bool expected)
    {
        // Arrange
        var leftResult = new PropositionBooleanResult<string>(
            left, 
            new MetadataNode<string>(left.ToString(), []), 
            new Explanation(left.ToString(), []),
            left.ToString());
        
        var rightResult = new  PropositionBooleanResult<string>(
            right, 
            new MetadataNode<string>(right.ToString(), []), 
            new Explanation(right.ToString(), []),
            right.ToString());

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
            new MetadataNode<string>(left.ToString(), []), 
            new Explanation(left.ToString(), []),
            left.ToString());
        
        var rightResult = new  PropositionBooleanResult<string>(
            right, 
            new MetadataNode<string>(right.ToString(), []), 
            new Explanation(right.ToString(), []),
            right.ToString());

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
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(true, true, false)]
    public void Should_support_xor_operation(bool left, bool right, bool expected)
    {
        // Arrange
        var leftResult = new PropositionBooleanResult<string>(
            left, 
            new MetadataNode<string>(left.ToString(), []), 
            new Explanation(left.ToString(), []),
            left.ToString());
        
        var rightResult = new  PropositionBooleanResult<string>(
            right, 
            new MetadataNode<string>(right.ToString(), []), 
            new Explanation(right.ToString(), []),
            right.ToString());

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
            new MetadataNode<string>(left.ToString(), []), 
            new Explanation(left.ToString(), []),
            left.ToString());
        
        var rightResult = new  PropositionBooleanResult<string>(
            right, 
            new MetadataNode<string>(right.ToString(), []), 
            new Explanation(right.ToString(), []),
            right.ToString());

        var result = leftResult ^ rightResult;

        var operands = new[] { leftResult, rightResult };

        // Act
        var act = result.Assertions;
        
        // Assert
        act.Should().Contain(operands.SelectMany(operand => operand.Explanation.Assertions));
    }

    [Theory]
    [InlineAutoData(false, true)]
    [InlineAutoData(true, false)]
    public void Should_support_not_operation(bool operand, bool expected)
    {
        // Arrange
        var operandResult = new  PropositionBooleanResult<string>(
            operand, 
            new MetadataNode<string>(operand.ToString(), []), 
            new Explanation(operand.ToString(), []),
            operand.ToString());

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
            new MetadataNode<string>(operand.ToString(), []), 
            new Explanation(operand.ToString(), []),
            operand.ToString());

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
}