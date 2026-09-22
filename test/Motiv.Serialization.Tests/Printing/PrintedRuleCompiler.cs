#if !NETFRAMEWORK
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Motiv.Serialization.Tests.Printing;

/// <summary>Compiles printed C# against Motiv and this test assembly, and invokes the printed method with defaults.</summary>
internal static class PrintedRuleCompiler
{
    private static readonly MetadataReference[] References = BuildReferences();

    public static Type Compile(string source, string @namespace, string className)
    {
        var compilation = CSharpCompilation.Create(
            $"printed-{Guid.NewGuid():N}",
            [CSharpSyntaxTree.ParseText(source)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
        {
            var diagnostics = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString());
            throw new InvalidOperationException($"printed C# did not compile:\n{string.Join("\n", diagnostics)}\n\n{source}");
        }

        var assembly = Assembly.Load(stream.ToArray());
        return assembly.GetType($"{@namespace}.{className}")
            ?? throw new InvalidOperationException($"'{className}' was not found in the printed assembly");
    }

    /// <summary>Invokes the printed method with the registry and every other parameter at its default.</summary>
    public static object Invoke(Type printed, string methodName, SpecRegistry registry)
    {
        var method = printed.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"'{methodName}' was not found on the printed class");
        var arguments = method.GetParameters()
            .Select(p => p.Position == 0 ? registry
                : p.HasDefaultValue ? p.DefaultValue
                : throw new InvalidOperationException($"the printed parameter '{p.Name}' has no default"))
            .ToArray();
        return method.Invoke(null, arguments)!;
    }

    private static MetadataReference[] BuildReferences()
    {
        var trusted = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        return trusted
            .Concat([typeof(Spec).Assembly.Location, typeof(SpecRegistry).Assembly.Location, typeof(PrintedRuleCompiler).Assembly.Location])
            .Distinct()
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
#endif
