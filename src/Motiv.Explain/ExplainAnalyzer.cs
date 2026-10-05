using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Motiv.Explain;

/// <summary>
///     Reports the <c>[Explain]</c> methods and uses that an explain build can't explain, so a missing log line
///     is never a surprise. It runs in every build. MOTIV1002 is the exception that only shows in explain builds,
///     because only they keep <c>[Explain]</c> in the other project's metadata.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExplainAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        ExplainDiagnostics.UsedAsDelegate,
        ExplainDiagnostics.CalledFromAnotherProject,
        ExplainDiagnostics.TypeNotPartial,
        ExplainDiagnostics.CannotExplain
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(
            AnalyzeDeclaration,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.LocalFunctionStatement);
        context.RegisterOperationAction(AnalyzeMethodReference, OperationKind.MethodReference);
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node, context.CancellationToken) is not IMethodSymbol method ||
            !ExplainAttribute.IsOn(method) ||
            // A partial method is judged on its implementing half, which has the body
            method.PartialImplementationPart is not null)
            return;

        if (ExplainEligibility.Check(method, context.Node, context.Compilation) is { } rejection)
            context.ReportDiagnostic(rejection.ToDiagnostic());
    }

    private static void AnalyzeMethodReference(OperationAnalysisContext context)
    {
        var method = ((IMethodReferenceOperation)context.Operation).Method.OriginalDefinition;
        if (ExplainAttribute.IsOn(method))
            context.ReportDiagnostic(Diagnostic.Create(
                ExplainDiagnostics.UsedAsDelegate, context.Operation.Syntax.GetLocation(), method.ToDisplayString()));
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var method = ((IInvocationOperation)context.Operation).TargetMethod.OriginalDefinition;
        if (!SymbolEqualityComparer.Default.Equals(method.ContainingAssembly, context.Compilation.Assembly) &&
            ExplainAttribute.IsOn(method))
            context.ReportDiagnostic(Diagnostic.Create(
                ExplainDiagnostics.CalledFromAnotherProject, context.Operation.Syntax.GetLocation(), method.ToDisplayString()));
    }
}
