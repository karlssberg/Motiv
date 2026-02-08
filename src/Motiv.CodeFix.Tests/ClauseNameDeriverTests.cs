using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.CodeFix.Tests;

public class ClauseNameDeriverTests
{
    [Fact]
    public void DeriveName_SimpleComparison_ReturnsSemanticName()
    {
        // age > 18
        var expression = ParseExpression("age > 18");

        var name = ClauseNameDeriver.DeriveName(expression, 1);

        Assert.Equal("IsAgeGreaterThan18", name);
    }

    [Fact]
    public void DeriveName_GreaterThanOrEqualZero_ReturnsNonNegativeName()
    {
        // valueA >= 0
        var expression = ParseExpression("m.ValueA >= 0");

        var name = ClauseNameDeriver.DeriveName(expression, 1);

        Assert.Equal("IsValueANonNegative", name);
    }

    [Fact]
    public void DeriveName_LessThanZero_ReturnsNegativeName()
    {
        // value < 0
        var expression = ParseExpression("value < 0");

        var name = ClauseNameDeriver.DeriveName(expression, 1);

        Assert.Equal("IsValueNegative", name);
    }

    [Fact]
    public void DeriveName_EqualsZero_ReturnsZeroName()
    {
        // count == 0
        var expression = ParseExpression("count == 0");

        var name = ClauseNameDeriver.DeriveName(expression, 1);

        Assert.Equal("IsCountZero", name);
    }

    [Fact]
    public void DeriveName_BooleanProperty_ReturnsPropertyName()
    {
        // isActive
        var expression = ParseExpression("isActive");

        var name = ClauseNameDeriver.DeriveName(expression, 1);

        Assert.Equal("IsActive", name);
    }

    [Fact]
    public void DeriveName_MemberAccess_ReturnsQualifiedName()
    {
        // order.Total > 100
        var expression = ParseExpression("m.Order.Total > 100");

        var name = ClauseNameDeriver.DeriveName(expression, 1);

        Assert.Equal("IsOrderTotalGreaterThan100", name);
    }

    [Fact]
    public void DeriveName_NullCheck_ReturnsNullCheckName()
    {
        // item != null
        var expression = ParseExpression("item != null");

        var name = ClauseNameDeriver.DeriveName(expression, 1);

        Assert.Equal("IsItemNotNull", name);
    }

    [Fact]
    public void DeriveName_IsPattern_FallsBackToClauseNumber()
    {
        // obj is string
        // Note: 'is' expressions are complex and fall back to generic names
        var expression = ParseExpression("obj is string");

        var name = ClauseNameDeriver.DeriveName(expression, 1);

        Assert.Equal("Clause1", name);
    }

    [Fact]
    public void DeriveName_Negation_ReturnsNegatedName()
    {
        // !isValid
        var expression = ParseExpression("!isValid");

        var name = ClauseNameDeriver.DeriveName(expression, 1);

        Assert.Equal("IsNotValid", name);
    }

    [Fact]
    public void DeriveName_ComplexExpression_FallsBackToClauseNumber()
    {
        // Complex expression that's hard to name
        var expression = ParseExpression("(a + b) * c > d / e && f % 2 == 0");

        var name = ClauseNameDeriver.DeriveName(expression, 3);

        Assert.Equal("Clause3", name);
    }

    [Fact]
    public void DeriveName_GreaterThan_UsesGreaterThanVerb()
    {
        // score > 75
        var expression = ParseExpression("score > 75");

        var name = ClauseNameDeriver.DeriveName(expression, 1);

        Assert.Equal("IsScoreGreaterThan75", name);
    }

    [Fact]
    public void DeriveName_LessThanOrEqual_UsesAtMostVerb()
    {
        // age <= 65
        var expression = ParseExpression("age <= 65");

        var name = ClauseNameDeriver.DeriveName(expression, 1);

        Assert.Equal("IsAgeAtMost65", name);
    }

    [Fact]
    public void DeriveName_GreaterThanOrEqual_UsesAtLeastVerb()
    {
        // age >= 18
        var expression = ParseExpression("age >= 18");

        var name = ClauseNameDeriver.DeriveName(expression, 1);

        Assert.Equal("IsAgeAtLeast18", name);
    }

    [Fact]
    public void DeriveName_NotEquals_UsesNotEqualsVerb()
    {
        // status != 0
        var expression = ParseExpression("status != 0");

        var name = ClauseNameDeriver.DeriveName(expression, 1);

        Assert.Equal("IsStatusNotZero", name);
    }

    [Fact]
    public void DeriveName_Equals_UsesIsVerb()
    {
        // type == 5
        var expression = ParseExpression("type == 5");

        var name = ClauseNameDeriver.DeriveName(expression, 1);

        Assert.Equal("IsType5", name);
    }

    [Fact]
    public void DeriveName_GreaterThanOne_UsesSpecificNumber()
    {
        // valueC >= 1
        var expression = ParseExpression("m.ValueC >= 1");

        var name = ClauseNameDeriver.DeriveName(expression, 1);

        Assert.Equal("IsValueCAtLeast1", name);
    }

    [Fact]
    public void DeriveName_TruncatesVeryLongNames()
    {
        // veryLongVariableNameThatExceedsReasonableLength > 100
        var expression = ParseExpression("veryLongVariableNameThatExceedsReasonableLength > 100");

        var name = ClauseNameDeriver.DeriveName(expression, 1);

        // Should either truncate or fall back
        Assert.True(name.Length <= 50 || name == "Clause1");
    }

    [Fact]
    public void DeriveName_LiteralOnLeft_PreservesExpressionOrder()
    {
        // 1 < valueC
        var expression = ParseExpression("1 < valueC");

        var name = ClauseNameDeriver.DeriveName(expression, 1);

        Assert.Equal("Is1LessThanValueC", name);
    }

    [Fact]
    public void DeriveName_LiteralOnLeftGreaterThan_PreservesExpressionOrder()
    {
        // 100 > age
        var expression = ParseExpression("100 > age");

        var name = ClauseNameDeriver.DeriveName(expression, 1);

        Assert.Equal("Is100GreaterThanAge", name);
    }

    [Fact]
    public void DeriveName_LiteralOnLeftWithMemberAccess_PreservesExpressionOrder()
    {
        // 5 <= m.Order.Total
        var expression = ParseExpression("5 <= m.Order.Total");

        var name = ClauseNameDeriver.DeriveName(expression, 1);

        Assert.Equal("Is5AtMostOrderTotal", name);
    }

    /// <summary>
    /// Helper to parse an expression string into an ExpressionSyntax.
    /// </summary>
    private static ExpressionSyntax ParseExpression(string expressionText)
    {
        var statement = SyntaxFactory.ParseStatement($"var x = {expressionText};");
        var localDecl = (LocalDeclarationStatementSyntax)statement;
        return localDecl.Declaration.Variables.First().Initializer!.Value;
    }
}
