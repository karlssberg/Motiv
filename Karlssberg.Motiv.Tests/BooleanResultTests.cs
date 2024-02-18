using FluentAssertions;
using Karlssberg.Motiv.Propositions;

namespace Karlssberg.Motiv.Tests;

public class BooleanResultTests
{
    [Theory]
    [InlineAutoData]
    public void Should_support_explicit_conversion_to_a_bool(bool isSatisfied, object metadata, string description)
    {
        var result = new BooleanResult<object>(isSatisfied, metadata, description);

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
        var leftResult = new BooleanResult<object>(left, new object(), left.ToString());
        var rightResult = new BooleanResult<object>(right, new object(), right.ToString());

        var act = leftResult & rightResult;

        var operands = new[] { leftResult, rightResult }
            .Where(operand => operand == act)
            .ToList();

        act.Satisfied.Should().Be(expected);
        act.Metadata.Should().HaveCount(operands.Count);
        act.Explanation.Reasons.Should().Contain(operands.SelectMany(operand => operand.Explanation.Reasons));
    }

    [Theory]
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(true, true, true)]
    public void Should_support_or_operation(bool left, bool right, bool expected)
    {
        var leftResult = new BooleanResult<object>(left, new object(), left.ToString());
        var rightResult = new BooleanResult<object>(right, new object(), right.ToString());

        var act = leftResult | rightResult;

        var operands = new[] { leftResult, rightResult }
            .Where(operand => operand == act)
            .ToList();

        act.Satisfied.Should().Be(expected);
        act.Metadata.Should().HaveCount(operands.Count);
        act.Explanation.Reasons.Should().Contain(operands.SelectMany(operand => operand.Explanation.Reasons));
    }

    [Theory]  
    [InlineAutoData(false, false, false)]
    [InlineAutoData(false, true, true)]
    [InlineAutoData(true, false, true)]
    [InlineAutoData(true, true, false)]
    public void Should_support_xor_operation(bool left, bool right, bool expected)
    {
        var leftResult = new BooleanResult<object>(left, new object(), left.ToString());
        var rightResult = new BooleanResult<object>(right, new object(), right.ToString());

        var act = leftResult ^ rightResult;

        var operands = new[] { leftResult, rightResult };

        act.Satisfied.Should().Be(expected);
        act.Metadata.Should().HaveCount(operands.Length);
        act.Explanation.Reasons.Should().Contain(operands.SelectMany(operand => operand.Explanation.Reasons));
    }



    [Theory]
    [InlineAutoData(false, true)]
    [InlineAutoData(true, false)]
    public void Should_support_not_operation(bool operand, bool expected)
    {
        var operandResult = new BooleanResult<object>(operand, new object(), operand.ToString());

        var act = !operandResult;

        act.Satisfied.Should().Be(expected);
        act.Metadata.Should().HaveCount(1);
        act.Explanation.Reasons.Should().Contain(operandResult.Explanation.Reasons);
    }
}