using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Motiv.CodeFix.Tests;

public static class CSharpCodeFixVerifier<TAnalyzer, TCodeFix>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
{
    public class Test : CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>
    {
        public Test()
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90;
            TestState.AdditionalReferences.Add(typeof(MotivCodeFixProvider).Assembly);
            TestState.AdditionalReferences.Add(typeof(Spec<>).Assembly);

            // Add the source for required types
            TestState.Sources.Add(
                """

                namespace System.Runtime.CompilerServices
                {
                    internal static class IsExternalInit {}
                }

                """);

            FixedState.Sources.Add(
                """

                namespace System.Runtime.CompilerServices
                {
                    internal static class IsExternalInit {}
                }

                """);
        }

        protected override CompilationOptions CreateCompilationOptions()
        {
            var compilationOptions = base.CreateCompilationOptions();
            return compilationOptions.WithSpecificDiagnosticOptions(
                compilationOptions.SpecificDiagnosticOptions.SetItems(GetNullableWarningsFromCompiler()));
        }

        public LanguageVersion LanguageVersion { get; set; } = LanguageVersion.Default;

        private static ImmutableDictionary<string, ReportDiagnostic> GetNullableWarningsFromCompiler()
        {
            string[] args = ["/warnaserror:nullable"];
            var commandLineArguments = CSharpCommandLineParser.Default.Parse(args,
                baseDirectory: Environment.CurrentDirectory, sdkDirectory: Environment.CurrentDirectory);
            var nullableWarnings = commandLineArguments.CompilationOptions.SpecificDiagnosticOptions;

            return nullableWarnings;
        }

        protected override ParseOptions CreateParseOptions()
        {
            return ((CSharpParseOptions)base.CreateParseOptions()).WithLanguageVersion(LanguageVersion);
        }
    }
}
