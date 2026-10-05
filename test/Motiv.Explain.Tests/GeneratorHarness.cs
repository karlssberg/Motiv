using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Motiv.Explain.Tests;

/// <summary>
///     Compiles a test program the way an MSBuild project with the Motiv.Explain package would: the
///     generator runs, and an explain build also gets what <c>build/Motiv.Explain.targets</c> adds, namely
///     the interceptor namespace and the <c>MOTIV_EXPLAIN</c> symbol. The result can be run in isolation.
/// </summary>
internal static class GeneratorHarness
{
    private static readonly ImmutableArray<MetadataReference> FrameworkReferences =
        [
            .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path))
        ];

    private static readonly MetadataReference MotivReference =
        MetadataReference.CreateFromFile(typeof(Spec).Assembly.Location);

    public static GeneratorRun Run(string source, bool explain, bool referenceMotiv = true)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest)
            .WithPreprocessorSymbols(explain ? new[] { "MOTIV_EXPLAIN" } : [])
            .WithFeatures(explain ? [new("InterceptorsNamespaces", "Motiv.Explain.Generated")] : []);

        var compilation = CSharpCompilation.Create(
            "ExplainTest" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source, parseOptions, path: "Program.cs")],
            referenceMotiv ? [.. FrameworkReferences, MotivReference] : FrameworkReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)
                .WithGeneralDiagnosticOption(ReportDiagnostic.Error));

        var driver = CSharpGeneratorDriver.Create(
            [new ExplainGenerator().AsSourceGenerator()],
            parseOptions: parseOptions,
            optionsProvider: new ExplainSwitch(explain));

        var ran = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);
        return new GeneratorRun(output, generatorDiagnostics, ran.GetRunResult());
    }

    private sealed class ExplainSwitch(bool explain) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(explain);
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new Options(false);
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new Options(false);

        private sealed class Options(bool explain) : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, out string value)
            {
                value = explain ? "true" : "";
                return explain && key == "build_property.MotivExplain";
            }
        }
    }
}

internal sealed record GeneratorRun(
    Compilation Output,
    ImmutableArray<Diagnostic> GeneratorDiagnostics,
    GeneratorDriverRunResult Result)
{
    public IEnumerable<string> GeneratedSources =>
        Result.GeneratedTrees.Select(tree => tree.ToString());

    /// <summary>Every warning or error from the generator and from compiling its output.</summary>
    public IReadOnlyList<Diagnostic> Problems =>
        GeneratorDiagnostics
            .Concat(Output.GetDiagnostics())
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .ToList();

    /// <summary>
    ///     Emits the compilation, loads it in its own context and invokes <c>Probe.Run()</c>, which the test
    ///     program defines and which returns the lines it wants to compare.
    /// </summary>
    public LoadedAssembly Load()
    {
        Problems.ShouldBeEmpty(string.Join(Environment.NewLine, Problems));
        using var stream = new MemoryStream();
        var emit = Output.Emit(stream);
        emit.Success.ShouldBeTrue(string.Join(Environment.NewLine, emit.Diagnostics));
        stream.Position = 0;
        var context = new AssemblyLoadContext(Output.AssemblyName, isCollectible: true);
        return new LoadedAssembly(context.LoadFromStream(stream));
    }
}

internal sealed record LoadedAssembly(Assembly Assembly)
{
    /// <summary>The probe's lines, with line endings normalised so the expectations hold on Windows too.</summary>
    public IReadOnlyList<string> Run() =>
        ((IEnumerable<string>)Assembly.GetType("Probe")!
            .GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, null)!)
        .Select(line => line.ReplaceLineEndings("\n"))
        .ToList();

    public IEnumerable<string> ReferencedAssemblyNames =>
        Assembly.GetReferencedAssemblies().Select(name => name.Name!);
}
