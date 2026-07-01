using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Motiv.Analyzer;

/// <summary>
/// A diagnostic analyzer that identifies boolean expressions that can be converted into Motiv propositions.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MotivAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The diagnostic descriptor for the MOTIV0001 diagnostic.
    /// </summary>
    public static readonly DiagnosticDescriptor Motiv0001 = new(
        "MOTIV0001",
        "Boolean Blindness",
        "Add provenance to the boolean expression",
        "Category",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Converts a boolean expression into logical proposition.");

    /// <summary>
    /// Gets the set of diagnostics that this analyzer can produce.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Motiv0001];

    /// <summary>
    /// Initializes the analyzer.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeLogicalExpression, SyntaxKind.GreaterThanExpression);
        context.RegisterSyntaxNodeAction(AnalyzeLogicalExpression, SyntaxKind.LessThanExpression);
        context.RegisterSyntaxNodeAction(AnalyzeLogicalExpression, SyntaxKind.GreaterThanOrEqualExpression);
        context.RegisterSyntaxNodeAction(AnalyzeLogicalExpression, SyntaxKind.LessThanOrEqualExpression);
        context.RegisterSyntaxNodeAction(AnalyzeLogicalExpression, SyntaxKind.EqualsExpression);
        context.RegisterSyntaxNodeAction(AnalyzeLogicalExpression, SyntaxKind.NotEqualsExpression);
        context.RegisterSyntaxNodeAction(AnalyzeLogicalExpression, SyntaxKind.LogicalAndExpression);
        context.RegisterSyntaxNodeAction(AnalyzeLogicalExpression, SyntaxKind.LogicalOrExpression);
        context.RegisterSyntaxNodeAction(AnalyzeLogicalExpression, SyntaxKind.LogicalNotExpression);
        context.RegisterSyntaxNodeAction(AnalyzeIsPatternExpression, SyntaxKind.IsExpression);
        context.RegisterSyntaxNodeAction(AnalyzeIsPatternExpression, SyntaxKind.IsPatternExpression);
    }

    private static void AnalyzeLogicalExpression(SyntaxNodeAnalysisContext context)
    {
        var expression = (ExpressionSyntax)context.Node;

        if (IsNestedInLogicalExpression(expression)) return;
        if (IsNestedInPatternExpression(expression)) return;

        // Check if this expression is inside a Spec.Build() lambda - if so, ignore it
        if (IsInsideSpecLambda(expression, context.SemanticModel)) return;

        // Negating a spec's evaluation result is MOTIV0007 territory
        if (expression is PrefixUnaryExpressionSyntax notExpression
            && NegatedSpecResultAnalyzer.IsSpecResultNegation(notExpression, context.SemanticModel))
            return;

        var diagnostic = Diagnostic.Create(Motiv0001, expression.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }

    internal static bool IsNestedInLogicalExpression(SyntaxNode node)
    {
        // Walk up through parenthesized expressions to find if we're inside a logical expression
        var parent = node.Parent;
        while (parent is ParenthesizedExpressionSyntax)
        {
            parent = parent.Parent;
        }

        return parent is BinaryExpressionSyntax
            or PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.ExclamationToken };
    }

    private static bool IsNestedInPatternExpression(SyntaxNode node)
    {
        // Walk up through parenthesized expressions
        var parent = node.Parent;
        while (parent is ParenthesizedExpressionSyntax)
        {
            parent = parent.Parent;
        }

        // Direct child of an is-pattern expression
        if (parent is IsPatternExpressionSyntax)
            return true;

        // Nested inside a pattern syntax node (e.g., property pattern, relational pattern)
        // This covers cases like `obj is { Value: > 5 }` where `> 5` is a GreaterThanExpression
        // inside a SubpatternSyntax inside a PropertyPatternClauseSyntax
        return node.FirstAncestorOrSelf<PatternSyntax>() is not null;
    }

    private static void AnalyzeIsPatternExpression(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node;

        if (IsNestedInLogicalExpression(node)) return;
        if (IsNestedInPatternExpression(node)) return;

        if (IsInsideSpecLambda(node, context.SemanticModel)) return;

        var diagnostic = Diagnostic.Create(Motiv0001, node.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsInsideSpecLambda(SyntaxNode node, SemanticModel semanticModel)
    {
        var lambda = node.FirstAncestorOrSelf<LambdaExpressionSyntax>();
        if (lambda is null) return false;

        return IsInsideSpecBuildInvocation(lambda, semanticModel)
            || IsInsideSpecBaseConstructor(lambda, semanticModel);
    }

    private static bool IsInsideSpecBuildInvocation(LambdaExpressionSyntax lambda, SemanticModel semanticModel)
    {
        var invocation = lambda.FirstAncestorOrSelf<InvocationExpressionSyntax>();

        if (invocation?.Expression is
            not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Build" } memberAccess)
            return false;

        if (memberAccess.Expression is IdentifierNameSyntax { Identifier.ValueText: "Spec" }
            or GenericNameSyntax { Identifier.ValueText: "Spec" })
            return true;

        return IsMotivSpecType(semanticModel, memberAccess.Expression);
    }

    private static bool IsInsideSpecBaseConstructor(LambdaExpressionSyntax lambda, SemanticModel semanticModel)
    {
        var baseType = lambda.FirstAncestorOrSelf<PrimaryConstructorBaseTypeSyntax>();
        if (baseType is null) return false;

        if (baseType.Type is GenericNameSyntax { Identifier.ValueText: "Spec" })
            return true;

        return IsMotivSpecType(semanticModel, baseType.Type);
    }

    private static bool IsMotivSpecType(SemanticModel semanticModel, SyntaxNode node)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(node);
        return symbolInfo.Symbol is INamedTypeSymbol
        {
            Name: "Spec",
            ContainingNamespace.Name: "Motiv"
        };
    }
}
