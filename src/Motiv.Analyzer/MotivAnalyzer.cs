using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Motiv.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MotivAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Motiv0001 = new(
        "MOTIV0001",
        "Convert to proposition",
        "The boolean expression can be converted to a logical proposition",
        "Category",
        DiagnosticSeverity.Hidden,
        isEnabledByDefault: true,
        description: "Converts a boolean expression into logical proposition.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Motiv0001];

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

        // Check if the binary expression is in a boolean method return statement
        var returnStatement = binaryExpression.FirstAncestorOrSelf<ReturnStatementSyntax>();
        if (returnStatement == null) return;

        var method = returnStatement.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method == null) return;

        // Check if this is a boolean method
        var returnType = method.ReturnType.ToString();
        if (returnType != "bool") return;

        // Report diagnostic for the boolean expression
        var diagnostic = Diagnostic.Create(Motiv0001, binaryExpression.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }
}
