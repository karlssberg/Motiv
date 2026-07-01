using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Motiv.Analyzer;

/// <summary>
/// A diagnostic analyzer that identifies negations of a spec's evaluation result, which discard
/// the negation from the proposition's explanation. Composing with <c>.Not()</c> instead keeps
/// the negation inside the proposition so its assertions reflect the decision actually made.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NegatedSpecResultAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The diagnostic descriptor for the MOTIV0007 diagnostic.
    /// </summary>
    public static readonly DiagnosticDescriptor Motiv0007 = new(
        "MOTIV0007",
        "Inverted spec result",
        "Negate the specification with .Not() instead of inverting its result",
        "Category",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Composes the negation into the proposition so that explanations reflect the negated outcome.");

    /// <summary>
    /// Gets the set of diagnostics that this analyzer can produce.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Motiv0007];

    /// <summary>
    /// Initializes the analyzer.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeLogicalNotExpression, SyntaxKind.LogicalNotExpression);
    }

    private static void AnalyzeLogicalNotExpression(SyntaxNodeAnalysisContext context)
    {
        var notExpression = (PrefixUnaryExpressionSyntax)context.Node;

        // Negations nested inside a larger logical expression are covered by MOTIV0001 on the root
        if (MotivAnalyzer.IsNestedInLogicalExpression(notExpression)) return;

        if (!IsSpecResultNegation(notExpression, context.SemanticModel)) return;

        var diagnostic = Diagnostic.Create(Motiv0007, notExpression.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }

    /// <summary>
    /// Determines whether the expression negates the <c>Satisfied</c> property of a Motiv spec
    /// evaluation, i.e. <c>!spec.Evaluate(model).Satisfied</c> (or the legacy
    /// <c>IsSatisfiedBy</c> equivalent).
    /// </summary>
    /// <param name="notExpression">The logical-not expression to inspect.</param>
    /// <param name="semanticModel">The semantic model used to resolve the evaluation method.</param>
    /// <returns><c>true</c> when the operand is a spec evaluation result.</returns>
    internal static bool IsSpecResultNegation(PrefixUnaryExpressionSyntax notExpression, SemanticModel semanticModel)
    {
        if (UnwrapParentheses(notExpression.Operand) is
            not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Satisfied" } satisfiedAccess)
            return false;

        if (UnwrapParentheses(satisfiedAccess.Expression) is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is
            not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Evaluate" or "IsSatisfiedBy" })
            return false;

        return semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol method
               && IsDeclaredOnSpecBase(method);
    }

    private static bool IsDeclaredOnSpecBase(IMethodSymbol method)
    {
        for (var type = method.ContainingType; type is not null; type = type.BaseType)
        {
            if (type is { Name: "SpecBase", ContainingNamespace.Name: "Motiv" })
                return true;
        }

        return false;
    }

    private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }
}
