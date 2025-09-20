using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Motiv.FluentFactory.Generator.Tests;

public static class CSharpSourceGeneratorVerifier<TSourceGenerator>
    where TSourceGenerator : IIncrementalGenerator, new()
{
    public class Test : CSharpSourceGeneratorTest<TSourceGenerator, DefaultVerifier>
    {
        public Test()
        {
            // Reference the generator assembly (for any shared types if needed)
            TestState.AdditionalReferences.Add(typeof(FluentFactoryGenerator).Assembly);
            // Reference the attributes assembly so test code can resolve attribute types
            TestState.AdditionalReferences.Add(typeof(Attributes.FluentConstructorAttribute).Assembly);

            // Add the source for required types and global aliases mapping old attribute names
            TestState.Sources.Add(
                """
                // Global aliases so tests written against the old namespace still compile
                global using Motiv.FluentFactory.Attributes;

                // Provide IsExternalInit for record-like features in older targets
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
