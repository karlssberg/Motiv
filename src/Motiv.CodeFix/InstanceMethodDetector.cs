using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.CodeFix;

/// <summary>
/// Result of instance method detection, containing both resolved and unresolved method references.
/// </summary>
public class InstanceMethodResult(
    IReadOnlyList<(InvocationExpressionSyntax Invocation, IMethodSymbol Method)> resolvedMethods,
    IReadOnlyList<string> unresolvedMethodNames)
{
    /// <summary>
    /// Instance method invocations that were successfully resolved by the semantic model.
    /// </summary>
    public IReadOnlyList<(InvocationExpressionSyntax Invocation, IMethodSymbol Method)> ResolvedMethods { get; } = resolvedMethods;

    /// <summary>
    /// Names of simple identifier invocations that could not be resolved — likely instance methods.
    /// </summary>
    public IReadOnlyList<string> UnresolvedMethodNames { get; } = unresolvedMethodNames;

    /// <summary>
    /// Whether any instance methods (resolved or unresolved) were detected.
    /// </summary>
    public bool HasInstanceMethods => ResolvedMethods.Count > 0 || UnresolvedMethodNames.Count > 0;

    /// <summary>
    /// All instance method names (both resolved and unresolved) as a set.
    /// </summary>
    public HashSet<string> AllMethodNames =>
        new HashSet<string>(ResolvedMethods.Select(m => m.Method.Name).Concat(UnresolvedMethodNames));
}

/// <summary>
/// Detects instance method invocations in expressions that require constructor injection.
/// </summary>
public class InstanceMethodDetector(SemanticModel semanticModel) : CSharpSyntaxWalker
{
    private List<(InvocationExpressionSyntax Invocation, IMethodSymbol Method)> InstanceMethods { get; } = [];
    private List<string> UnresolvedNames { get; } = [];

    /// <summary>
    /// Detects all instance method invocations in the expression that belong to the containing class.
    /// </summary>
    /// <param name="node">The syntax node to analyze.</param>
    /// <param name="containingType">The containing type to check for instance methods.</param>
    /// <returns>Detection result containing both resolved and unresolved instance methods.</returns>
    public InstanceMethodResult Detect(
        SyntaxNode node,
        INamedTypeSymbol containingType)
    {
        InstanceMethods.Clear();
        UnresolvedNames.Clear();
        ContainingType = containingType;
        Visit(node);
        return new InstanceMethodResult(
            InstanceMethods.ToList(),
            UnresolvedNames.ToList());
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
        else if (symbolInfo.Symbol is null
                 && symbolInfo.CandidateSymbols.IsEmpty
                 && node.Expression is IdentifierNameSyntax unresolvedId)
        {
            // Unresolved simple identifier invocations are likely instance methods
            UnresolvedNames.Add(unresolvedId.Identifier.ValueText);
        }

        base.VisitInvocationExpression(node);
    }
}
