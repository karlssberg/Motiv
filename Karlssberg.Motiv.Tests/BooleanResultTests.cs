using FluentAssertions;

namespace Karlssberg.Motiv.Tests;

public class BooleanResultTests
{
    [Theory]
    [AutoParams]
    public void Should_support_explicit_conversion_to_a_bool(bool isSatisfied, object metadata, string description)
    {
        var result = new BooleanResult<object>(isSatisfied, metadata, description);
        
        var act = (bool) result;
        
        act.Should().Be(isSatisfied);
    }
    
    [Theory]
    [AutoParams(false, false, false)]
    [AutoParams(false, true, false)]
    [AutoParams(true, false, false)]
    [AutoParams(true, true, true)]
    public void Should_support_and_operation(bool left, bool right, bool expected)
    {
        var leftResult = new BooleanResult<object>(left, new object(), left.ToString());
        var rightResult = new BooleanResult<object>(right, new object(), right.ToString());
        
        var act = leftResult & rightResult;

        var operands = new [] {leftResult, rightResult}
            .Where(operand => operand == act)
            .ToList();
        
        act.IsSatisfied.Should().Be(expected);
        act.GetInsights().Should().HaveCount(operands.Count);
        act.Reasons.Should().Contain(operands.SelectMany(operand => operand.Reasons));
    }
    
    [Theory]
    [AutoParams(false, false, false)]
    [AutoParams(false, true, true)]
    [AutoParams(true, false, true)]
    [AutoParams(true, true, true)]
    public void Should_support_or_operation(bool left, bool right, bool expected)
    {
        var leftResult = new BooleanResult<object>(left, new object(), left.ToString());
        var rightResult = new BooleanResult<object>(right, new object(), right.ToString());
        
        var act = leftResult | rightResult;

        var operands = new [] {leftResult, rightResult}
            .Where(operand => operand == act)
            .ToList();
        
        act.IsSatisfied.Should().Be(expected);
        act.GetInsights().Should().HaveCount(operands.Count);
        act.Reasons.Should().Contain(operands.SelectMany(operand => operand.Reasons));
    }
    
    [Theory]
    [AutoParams(false, false, false)]
    [AutoParams(false, true, true)]
    [AutoParams(true, false, true)]
    [AutoParams(true, true, false)]
    public void Should_support_xor_operation(bool left, bool right, bool expected)
    {
        var leftResult = new BooleanResult<object>(left, new object(), left.ToString());
        var rightResult = new BooleanResult<object>(right, new object(), right.ToString());
        
        var act = leftResult ^ rightResult;

        var operands = new [] {leftResult, rightResult};
        
        act.IsSatisfied.Should().Be(expected);
        act.GetInsights().Should().HaveCount(operands.Length);
        act.Reasons.Should().Contain(operands.SelectMany(operand => operand.Reasons));
    }
    
    
    
    [Theory]
    [AutoParams(false, true)]
    [AutoParams(true, false)]
    public void Should_support_not_operation(bool operand, bool expected)
    {
        var operandResult = new BooleanResult<object>(operand, new object(), operand.ToString());

        var act = !operandResult;
        
        act.IsSatisfied.Should().Be(expected);
        act.GetInsights().Should().HaveCount(1);
        act.Reasons.Should().Contain(operandResult.Reasons);
    }
}