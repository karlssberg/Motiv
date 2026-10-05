using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Motiv.Explain;

/// <summary>
///     In an explain build (<c>MotivExplain=true</c>), intercepts each direct call to an <c>[Explain]</c>
///     method with one that evaluates the method's expression as a decomposed Motiv spec and logs the
///     justification. In any other build it adds only the attribute, so calls bind to the original method
///     and the assembly never references Motiv.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ExplainGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(output =>
            output.AddSource("ExplainAttribute.g.cs", ExplainAttribute.Source));

        var enabled = context.AnalyzerConfigOptionsProvider.Select((options, _) =>
            options.GlobalOptions.TryGetValue("build_property.MotivExplain", out var value) &&
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));

        context.RegisterSourceOutput(enabled, (output, isEnabled) =>
        {
            if (isEnabled)
                output.AddSource("MotivExplainLog.g.cs", ExplainSourceWriter.LogSource);
        });

        // Only where each marked method is: cheap, and all a build without the switch ever computes
        var marked = context.SyntaxProvider.ForAttributeWithMetadataName(
                ExplainAttribute.MetadataName,
                static (node, _) => node is MethodDeclarationSyntax,
                static (target, _) => (target.TargetNode.SyntaxTree, target.TargetNode.Span))
            .Collect();

        var input = marked.Combine(enabled).Combine(context.CompilationProvider);
        context.RegisterSourceOutput(input, (output, pair) =>
        {
            var ((locations, isEnabled), compilation) = pair;
            if (!isEnabled || locations.IsEmpty)
                return;

            foreach (var target in FindTargets(compilation, locations, output.CancellationToken))
                output.AddSource(ExplainSourceWriter.HintName(target), ExplainSourceWriter.Write(target));
        });
    }

    private static IEnumerable<ExplainTarget> FindTargets(
        Compilation compilation,
        IEnumerable<(SyntaxTree Tree, TextSpan Span)> locations,
        CancellationToken cancellationToken)
    {
        var methods = locations
            .Select(location => Accept(compilation, location, cancellationToken))
            .Where(method => method is not null)
            .Select(method => method!)
            .ToList();

        var calls = FindCalls(compilation, methods.Select(method => method.Symbol), cancellationToken);

        // Overloads share a name, so each gets its position among its namesakes in the type as a suffix
        return methods
            .GroupBy(method => (Type: method.Symbol.ContainingType, method.Symbol.Name), TypeAndName.Comparer)
            .SelectMany(namesakes => namesakes.Select((method, ordinal) => (method, ordinal)))
            .Where(entry => calls.ContainsKey(entry.method.Symbol))
            .Select(entry => new ExplainTarget(
                entry.method.Symbol,
                entry.method.Declaration,
                entry.method.Body,
                entry.method.Model,
                entry.ordinal,
                calls[entry.method.Symbol]));
    }

    private static Candidate? Accept(Compilation compilation, (SyntaxTree Tree, TextSpan Span) location, CancellationToken cancellationToken)
    {
        if (location.Tree.GetRoot(cancellationToken).FindNode(location.Span) is not MethodDeclarationSyntax declaration)
            return null;
        var model = compilation.GetSemanticModel(location.Tree);
        if (model.GetDeclaredSymbol(declaration, cancellationToken) is not { } symbol)
            return null;
        return ExplainEligibility.Accept(symbol, declaration, compilation) is { } body
            ? new Candidate(symbol, declaration, body, model)
            : null;
    }

    private static Dictionary<IMethodSymbol, List<string>> FindCalls(
        Compilation compilation, IEnumerable<IMethodSymbol> methods, CancellationToken cancellationToken)
    {
        var targets = new HashSet<IMethodSymbol>(methods, SymbolEqualityComparer.Default);
        var names = new HashSet<string>(targets.Select(method => method.Name));
        var calls = new Dictionary<IMethodSymbol, List<string>>(SymbolEqualityComparer.Default);

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var invocations = tree.GetRoot(cancellationToken).DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(invocation => InvokedName(invocation) is { } name && names.Contains(name));

            foreach (var invocation in invocations)
            {
                if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol called ||
                    !targets.Contains(called) ||
                    model.GetInterceptableLocation(invocation, cancellationToken) is not { } location)
                    continue;

                if (!calls.TryGetValue(called, out var attributes))
                    calls[called] = attributes = [];
                attributes.Add(location.GetInterceptsLocationAttributeSyntax());
            }
        }

        return calls;
    }

    private static string? InvokedName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        SimpleNameSyntax name => name.Identifier.ValueText,
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
        _ => null
    };

    private sealed record Candidate(IMethodSymbol Symbol, MethodDeclarationSyntax Declaration, ExpressionSyntax Body, SemanticModel Model);

    private sealed class TypeAndName : IEqualityComparer<(INamedTypeSymbol Type, string Name)>
    {
        public static readonly TypeAndName Comparer = new();

        public bool Equals((INamedTypeSymbol Type, string Name) x, (INamedTypeSymbol Type, string Name) y) =>
            SymbolEqualityComparer.Default.Equals(x.Type, y.Type) && x.Name == y.Name;

        public int GetHashCode((INamedTypeSymbol Type, string Name) obj) =>
            SymbolEqualityComparer.Default.GetHashCode(obj.Type) ^ obj.Name.GetHashCode();
    }
}
