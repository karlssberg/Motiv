using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.CodeFix.Tests;

public class LogicalChainGrouperTests
{
    [Fact]
    public void Group_OrChainWithInvocations_GroupsFromFirstInvocation()
    {
        var expression = ParseExpression("a || b || IsGreen(text) || c");

        var result = LogicalChainGrouper.Group(expression);

        Assert.Equal("a || b || (IsGreen(text) || c)", Normalize(result));
    }

    [Fact]
    public void Group_OrChainWithInvocationAtStart_ReturnsUnchanged()
    {
        var expression = ParseExpression("IsGreen(text) || a || b");

        var result = LogicalChainGrouper.Group(expression);

        Assert.Equal("IsGreen(text) || a || b", Normalize(result));
    }

    [Fact]
    public void Group_OrChainWithSingleInvocationAtEnd_ReturnsUnchanged()
    {
        var expression = ParseExpression("a || b || IsGreen(text)");

        var result = LogicalChainGrouper.Group(expression);

        Assert.Equal("a || b || IsGreen(text)", Normalize(result));
    }

    [Fact]
    public void Group_AndChainWithInvocations_GroupsCorrectly()
    {
        var expression = ParseExpression("a && b && IsGreen(text) && c");

        var result = LogicalChainGrouper.Group(expression);

        Assert.Equal("a && b && (IsGreen(text) && c)", Normalize(result));
    }

    [Fact]
    public void Group_MixedAndInsideOr_RecursivelyGroups()
    {
        var expression = ParseExpression("x || (a && b && IsGreen(text) && c)");

        var result = LogicalChainGrouper.Group(expression);

        Assert.Equal("x || (a && b && (IsGreen(text) && c))", Normalize(result));
    }

    [Fact]
    public void Group_NoInvocations_ReturnsUnchanged()
    {
        var expression = ParseExpression("a && b && c");

        var result = LogicalChainGrouper.Group(expression);

        Assert.Equal("a && b && c", Normalize(result));
    }

    [Fact]
    public void Group_OrChainNoInvocations_ReturnsUnchanged()
    {
        var expression = ParseExpression("a || b || c");

        var result = LogicalChainGrouper.Group(expression);

        Assert.Equal("a || b || c", Normalize(result));
    }

    private static string Normalize(ExpressionSyntax expression) =>
        expression.NormalizeWhitespace().ToFullString();

    private static ExpressionSyntax ParseExpression(string expressionText)
    {
        var statement = SyntaxFactory.ParseStatement($"var x = {expressionText};");
        var localDecl = (LocalDeclarationStatementSyntax)statement;
        return localDecl.Declaration.Variables.First().Initializer!.Value;
    }
}
