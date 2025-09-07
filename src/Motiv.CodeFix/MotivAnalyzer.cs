using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Motiv.CodeFix
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MotivAnalyzer : DiagnosticAnalyzer
    {
        public static readonly DiagnosticDescriptor Rule = new(
            "MOTIV0001",
            "Convert to Spec to address boolean blindness",
            "Your rule message format",
            "Category",
            DiagnosticSeverity.Hidden,
            isEnabledByDefault: true,
            description: "Description of what this rule checks for.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeSyntaxNode, SyntaxKind.ClassDeclaration);
        }

        private static void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
        {
            // Your analysis logic here
            // Report diagnostics using: context.ReportDiagnostic(...)
        }
    }
}
