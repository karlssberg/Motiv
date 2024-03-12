using FluentAssertions;
using Karlssberg.Motiv.FirstOrder;

namespace Karlssberg.Motiv.Tests;

public class BooleanResultTests
{
    [Theory]
    [InlineAutoData]
    public void Should_support_explicit_conversion_to_a_bool(
        bool isSatisfied,
        object metadata,
        IProposition proposition)
    {
        var result = new BooleanResult<object>(isSatisfied, metadata, proposition);

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
        var leftResult = new BooleanResult<object>(left, new object(), new Proposition(left.ToString()));
        var rightResult = new BooleanResult<object>(right, new object(), new Proposition(right.ToString()));

        var act = leftResult & rightResult;

        var operands = new[] { leftResult, rightResult }
            .Where(operand => operand == act)
            .ToList();

        act.Satisfied.Should().Be(expected);
        act.MetadataTree.Should().HaveCount(operands.Count);
        act.Explanation.Assertions.Should().Contain(operands.SelectMany(operand => operand.Explanation.Assertions));
    }

    [Theory]
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(true, true, true)]
    public void Should_support_or_operation(bool left, bool right, bool expected)
    {
        var leftResult = new BooleanResult<object>(left, new object(), new Proposition(left.ToString()));
        var rightResult = new BooleanResult<object>(right, new object(), new Proposition(right.ToString()));

        var act = leftResult | rightResult;

        var operands = new[] { leftResult, rightResult }
            .Where(operand => operand == act)
            .ToList();

        act.Satisfied.Should().Be(expected);
        act.MetadataTree.Should().HaveCount(operands.Count);
        act.Explanation.Assertions.Should().Contain(operands.SelectMany(operand => operand.Explanation.Assertions));
    }

    [Theory]
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(true, true, false)]
    public void Should_support_xor_operation(bool left, bool right, bool expected)
    {
        var leftResult = new BooleanResult<object>(left, new object(), new Proposition(left.ToString()));
        var rightResult = new BooleanResult<object>(right, new object(), new Proposition(right.ToString()));

        var act = leftResult ^ rightResult;

        var operands = new[] { leftResult, rightResult };

        act.Satisfied.Should().Be(expected);
        act.MetadataTree.Should().HaveCount(operands.Length);
        act.Explanation.Assertions.Should().Contain(operands.SelectMany(operand => operand.Explanation.Assertions));
    }

    [Theory]
    [InlineAutoData(false, true)]
    [InlineAutoData(true, false)]
    public void Should_support_not_operation(bool operand, bool expected)
    {
        var operandResult = new BooleanResult<object>(operand, new object(), new Proposition(operand.ToString()));

        var act = !operandResult;

        act.Satisfied.Should().Be(expected);
        act.MetadataTree.Should().HaveCount(1);
        act.Explanation.Assertions.Should().Contain(operandResult.Explanation.Assertions);
    }

    [Theory]
    [InlineAutoData(true, "underlying is true")]
    [InlineAutoData(false, "underlying is false")]
    public void Should_generate_sub_assertions(bool model, string expected)
    {
        var underlyingSpec = Spec
            .Build<bool>(m => m)
            .WhenTrue("underlying is true")
            .WhenFalse("underlying is false")
            .CreateSpec();

        var spec = Spec
            .Build(underlyingSpec)
            .CreateSpec("top-level proposition");

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
            .Build<bool>(m => m)
            .CreateSpec("left")
            .IsSatisfiedBy(leftSatisfied);
        
        var rightResult= Spec
            .Build<bool>(m => m)
            .CreateSpec("right")
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
            .Build<bool>(m => m)
            .CreateSpec("left")
            .IsSatisfiedBy(leftSatisfied);
        
        var rightResult= Spec
            .Build<bool>(m => m)
            .WhenTrue(true)
            .WhenFalse(false)
            .CreateSpec("right")
            .IsSatisfiedBy(rightSatisfied);
        
        var act = leftResult == rightResult;
        
        act.Should().Be(expected);
    }
}