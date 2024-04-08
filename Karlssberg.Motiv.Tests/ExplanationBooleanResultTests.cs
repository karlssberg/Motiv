using FluentAssertions;
using Karlssberg.Motiv.BasicProposition;

namespace Karlssberg.Motiv.Tests;

public class ExplanationBooleanResultTests
{
    [Theory]
    [InlineAutoData]
    public void Should_support_explicit_conversion_to_a_bool(
        bool isSatisfied,
        string because)
    {
        var result = new PropositionBooleanResult<string>(isSatisfied, because, because);

        var act = (bool)result;

        act.Should().Be(isSatisfied);
    }

    [Theory]
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, false)]
    [InlineAutoData(true, false, false)]
    [InlineAutoData(true, true, true)]
    public void Should_support_and_operation(bool left, bool right, bool expected)
    {
        var leftResult = new PropositionBooleanResult<string>(left, left.ToString(), left.ToString());
        var rightResult = new  PropositionBooleanResult<string>(right, right.ToString(), right.ToString());

        var act = leftResult & rightResult;

        var operands = new[] { leftResult, rightResult }
            .Where(operand => operand == act)
            .ToList();

        act.Satisfied.Should().Be(expected);
        act.Assertions.Should().Contain(operands.GetAssertions());
    }

    [Theory]
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(true, true, true)]
    public void Should_support_or_operation(bool left, bool right, bool expected)
    {
        var leftResult = new  PropositionBooleanResult<string>(left, left.ToString(), left.ToString());
        var rightResult = new  PropositionBooleanResult<string>(right, right.ToString(), right.ToString());

        var act = leftResult | rightResult;

        var operands = new[] { leftResult, rightResult }
            .Where(operand => operand == act)
            .ToList();

        act.Satisfied.Should().Be(expected);
        act.Assertions.Should().Contain(operands.SelectMany(operand => operand.Explanation.Assertions));
    }

    [Theory]
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(true, true, false)]
    public void Should_support_xor_operation(bool left, bool right, bool expected)
    {
        var leftResult = new  PropositionBooleanResult<string>(left, left.ToString(), left.ToString());
        var rightResult = new  PropositionBooleanResult<string>(right, right.ToString(), right.ToString());

        var act = leftResult ^ rightResult;

        var operands = new[] { leftResult, rightResult };

        act.Satisfied.Should().Be(expected);
        act.Assertions.Should().Contain(operands.SelectMany(operand => operand.Explanation.Assertions));
    }

    [Theory]
    [InlineAutoData(false, true)]
    [InlineAutoData(true, false)]
    public void Should_support_not_operation(bool operand, bool expected)
    {
        var operandResult = new  PropositionBooleanResult<string>(operand, operand.ToString(), operand.ToString());

        var act = !operandResult;

        act.Satisfied.Should().Be(expected);
        act.MetadataTree.Should().HaveCount(1);
        act.Assertions.Should().Contain(operandResult.Explanation.Assertions);
    }

    [Theory]
    [InlineAutoData(true, "underlying is true")]
    [InlineAutoData(false, "underlying is false")]
    public void Should_generate_sub_assertions(bool model, string expected)
    {
        var underlyingSpec = Spec
            .Build((bool m) => m)
            .WhenTrue("underlying is true")
            .WhenFalse("underlying is false")
            .Create();

        var spec = Spec
            .Build(underlyingSpec)
            .Create("top-level proposition");

        var act = spec.IsSatisfiedBy(model);

        act.SubAssertions.Should().BeEquivalentTo(expected);
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
        var leftResult = Spec
            .Build((bool m) => m)
            .Create("left")
            .IsSatisfiedBy(leftSatisfied);
        
        var rightResult= Spec
            .Build((bool m) => m)
            .Create("right")
            .IsSatisfiedBy(rightSatisfied);
        
        var act = leftResult.Equals(rightResult);
        
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
        
        var act = leftResult == rightResult;
        
        act.Should().Be(expected);
    }
}