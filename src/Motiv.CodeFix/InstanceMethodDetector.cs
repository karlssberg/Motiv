using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.CodeFix;

/// <summary>
/// Detects instance method invocations in expressions that require constructor injection.
/// </summary>
public class InstanceMethodDetector(SemanticModel semanticModel) : CSharpSyntaxWalker
{
    private List<(InvocationExpressionSyntax Invocation, IMethodSymbol Method)> InstanceMethods { get; } = [];

    /// <summary>
    /// Gets all instance method invocations in the expression that belong to the containing class.
    /// </summary>
    /// <param name="node">The syntax node to analyze.</param>
    /// <param name="containingType">The containing type to check for instance methods.</param>
    /// <returns>List of instance method invocations.</returns>
    public IReadOnlyList<(InvocationExpressionSyntax Invocation, IMethodSymbol Method)> GetInstanceMethods(
        SyntaxNode node,
        INamedTypeSymbol containingType)
    {
        InstanceMethods.Clear();
        ContainingType = containingType;
        Visit(node);
        return InstanceMethods;
    }

    private INamedTypeSymbol? ContainingType { get; set; }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(node);

        if (symbolInfo.Symbol is IMethodSymbol { IsStatic: false } methodSymbol
            && SymbolEqualityComparer.Default.Equals(methodSymbol.ContainingType, ContainingType)
            && node.Expression is IdentifierNameSyntax)
        {
            // Check if it's a simple invocation (not qualified with 'this')
            InstanceMethods.Add((node, methodSymbol));
        }

        base.VisitInvocationExpression(node);
    }
}
