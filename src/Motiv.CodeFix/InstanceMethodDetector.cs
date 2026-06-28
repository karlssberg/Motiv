using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.CodeFix;

/// <summary>
/// Result of instance method detection, containing both resolved and unresolved method references.
/// </summary>
public class InstanceMethodResult(
    IReadOnlyList<(InvocationExpressionSyntax Invocation, IMethodSymbol Method)> resolvedMethods,
    IReadOnlyList<string> unresolvedMethodNames,
    IReadOnlyList<(InvocationExpressionSyntax Invocation, IMethodSymbol Method)> staticMethods)
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
    /// Static method invocations from the containing class.
    /// </summary>
    public IReadOnlyList<(InvocationExpressionSyntax Invocation, IMethodSymbol Method)> StaticMethods { get; } = staticMethods;

    /// <summary>
    /// Whether any instance methods (resolved or unresolved) were detected.
    /// </summary>
    public bool HasInstanceMethods => ResolvedMethods.Count > 0 || UnresolvedMethodNames.Count > 0;

    /// <summary>
    /// All instance method names (both resolved and unresolved) as a set.
    /// </summary>
    public HashSet<string> AllMethodNames { get; } =
        [..resolvedMethods.Select(m => m.Method.Name).Concat(unresolvedMethodNames)];

    /// <summary>
    /// All static method names from the containing class as a set.
    /// </summary>
    public HashSet<string> StaticMethodNames { get; } =
        [..staticMethods.Select(m => m.Method.Name)];
}

/// <summary>
/// Detects instance method invocations in expressions that require constructor injection.
/// </summary>
public class InstanceMethodDetector(SemanticModel semanticModel) : CSharpSyntaxWalker
{
    private List<(InvocationExpressionSyntax Invocation, IMethodSymbol Method)> InstanceMethods { get; } = [];
    private List<(InvocationExpressionSyntax Invocation, IMethodSymbol Method)> StaticMethodsList { get; } = [];
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
        StaticMethodsList.Clear();
        UnresolvedNames.Clear();
        ContainingType = containingType;
        Visit(node);
        return new InstanceMethodResult(
            InstanceMethods.ToList(),
            UnresolvedNames.ToList(),
            StaticMethodsList.ToList());
    }

    private INamedTypeSymbol? ContainingType { get; set; }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(node);

        switch (symbolInfo.Symbol)
        {
            case IMethodSymbol { IsStatic: false } methodSymbol
                when SymbolEqualityComparer.Default.Equals(methodSymbol.ContainingType, ContainingType)
                     && node.Expression is IdentifierNameSyntax:
                // Check if it's a simple invocation (not qualified with 'this')
                InstanceMethods.Add((node, methodSymbol));
                break;

            case IMethodSymbol { IsStatic: true } staticMethodSymbol
                when SymbolEqualityComparer.Default.Equals(staticMethodSymbol.ContainingType, ContainingType)
                     && node.Expression is IdentifierNameSyntax:
                StaticMethodsList.Add((node, staticMethodSymbol));
                break;

            case null
                when symbolInfo.CandidateSymbols.IsEmpty
                     && node.Expression is IdentifierNameSyntax unresolvedId
                     && !IsLanguageKeyword(unresolvedId.Identifier.ValueText):
                // Unresolved simple identifier invocations are likely instance methods
                UnresolvedNames.Add(unresolvedId.Identifier.ValueText);
                break;
        }

        base.VisitInvocationExpression(node);
    }

    /// <summary>
    /// Checks whether the identifier is a C# contextual keyword that is syntactically an invocation
    /// (e.g., <c>nameof(X)</c>) but should not be treated as a method call.
    /// </summary>
    private static bool IsLanguageKeyword(string identifier) =>
        SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None;
}
