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
        context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.GreaterThanExpression);
        context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.LessThanExpression);
        context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.GreaterThanOrEqualExpression);
        context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.LessThanOrEqualExpression);
        context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.EqualsExpression);
        context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.NotEqualsExpression);
        context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.LogicalAndExpression);
        context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.LogicalOrExpression);
    }

    private static void AnalyzeBinaryExpression(SyntaxNodeAnalysisContext context)
    {
        var binaryExpression = (BinaryExpressionSyntax)context.Node;

        if (binaryExpression.Parent is BinaryExpressionSyntax) return;

        // Check if this expression is inside a Spec.Build() lambda - if so, ignore it
        if (IsInsideSpecBuildLambda(binaryExpression, context.SemanticModel)) return;

        // Report diagnostic for the boolean expression
        var diagnostic = Diagnostic.Create(Motiv0001, binaryExpression.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsInsideSpecBuildLambda(SyntaxNode node, SemanticModel semanticModel)
    {
        // Walk up the syntax tree to find if we're inside a lambda expression
        var lambda = node.FirstAncestorOrSelf<LambdaExpressionSyntax>();

        // Check if the lambda is an argument to Spec.Build()
        var invocation = lambda?.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is null) return false;

        // Check if this is a call to Spec.Build with strong typing
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.ValueText == "Build")
        {
            // Use semantic model to get the symbol for the expression (should be Spec)
            var symbolInfo = semanticModel.GetSymbolInfo(memberAccess.Expression);
            if (symbolInfo.Symbol is INamedTypeSymbol typeSymbol)
            {
                // Check if the type is Motiv.Spec
                return typeSymbol.ContainingNamespace.Name == "Motiv" &&
                       typeSymbol.Name == "Spec";
            }
        }

        return false;
    }
}
